using System.Text.Json;
using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// Reviewed mode: mở link_review → click banner đầu → lightbox → chụp preview → Gemini → Mongo.
/// Duyệt hết bằng nút "Display the next ad."; creative_id đã có trong Mongo thì bỏ qua.
/// </summary>
public sealed class GamAdReviewCenterWorkflow : IGamAdReviewWorkflow
{
    private readonly ChromeDriverFactory _chromeDriverFactory;
    private readonly ReviewBannerUseCase _useCase;
    private readonly IBannerReviewRepository _repository;
    private readonly ITelegramNotifier _telegram;
    private readonly GamReviewSettings _gamSettings;

    public GamAdReviewCenterWorkflow(
        ChromeDriverFactory chromeDriverFactory,
        ReviewBannerUseCase useCase,
        IBannerReviewRepository repository,
        ITelegramNotifier telegram,
        IOptions<GamReviewSettings> gamSettings)
    {
        _chromeDriverFactory = chromeDriverFactory;
        _useCase = useCase;
        _repository = repository;
        _telegram = telegram;
        _gamSettings = gamSettings.Value;
    }

    public async Task<GamReviewWorkflowResult> ProcessReviewListAsync(
        string listUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(listUrl))
            throw new ArgumentException("link_review không được rỗng.", nameof(listUrl));

        ChromeDriver? driver = null;
        var processed = 0;
        var skipped = 0;
        var reviewed = 0;
        var errors = 0;

        try
        {
            driver = _chromeDriverFactory.CreateResilient();
            driver.Navigate().GoToUrl(listUrl);

            var gridWait = CreateWait(driver, _gamSettings.GridWaitSeconds);
            var firstCard = gridWait.Until(WaitUntilVisible(By.XPath(GamReviewSelectors.FirstAdPreviewCard)));
            firstCard.Click();

            var lightboxWait = CreateWait(driver, _gamSettings.LightboxWaitSeconds);
            lightboxWait.Until(WaitUntilVisible(By.XPath(GamReviewSelectors.LightboxCreativePreview)));

            string? previousCreativeId = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var creativeId = ReadCreativeId(driver);
                    if (string.IsNullOrWhiteSpace(creativeId))
                    {
                        Console.Error.WriteLine("Không đọc được creative_id — dừng vòng lặp.");
                        errors++;
                        break;
                    }

                    if (creativeId == previousCreativeId)
                    {
                        Console.WriteLine($"creative_id lặp ({creativeId}) — hết danh sách.");
                        break;
                    }

                    previousCreativeId = creativeId;
                    processed++;

                    if (await _repository.ExistsByCreativeIdAsync(creativeId, cancellationToken))
                    {
                        Console.WriteLine($"Skip creative_id đã tồn tại: {creativeId}");
                        skipped++;
                    }
                    else
                    {
                        var outcome = await ReviewAndSaveAsync(driver, creativeId, cancellationToken);
                        if (outcome)
                            reviewed++;
                        else
                            errors++;

                        if (_gamSettings.GeminiDelaySeconds > 0)
                            await Task.Delay(TimeSpan.FromSeconds(_gamSettings.GeminiDelaySeconds), cancellationToken);
                    }

                    if (!TryClickNextAd(driver))
                        break;

                    lightboxWait.Until(d =>
                    {
                        var id = ReadCreativeId(d);
                        return !string.IsNullOrWhiteSpace(id) && id != creativeId;
                    });
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.Error.WriteLine($"Lỗi xử lý banner: {ex.Message}");
                    await _telegram.NotifyExceptionAsync(nameof(ProcessReviewListAsync), ex, cancellationToken: cancellationToken);
                    if (!TryClickNextAd(driver))
                        break;
                }
            }

            return new GamReviewWorkflowResult
            {
                ProcessedCount = processed,
                SkippedExistingCount = skipped,
                ReviewedCount = reviewed,
                ErrorCount = errors
            };
        }
        finally
        {
            try { driver?.Quit(); } catch { }
            try { driver?.Dispose(); } catch { }
        }
    }

    private async Task<bool> ReviewAndSaveAsync(
        ChromeDriver driver,
        string creativeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = driver.FindElement(By.XPath(GamReviewSelectors.LightboxCreativePreview));
            var imagePath = SaveElementScreenshot(preview, creativeId);

            var outcome = await _useCase.ExecuteAsync(imagePath, cancellationToken);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var iframeId = TryReadIframeId(driver);

            var (isBlocked, note) = MapOutcome(outcome);
            var doc = new BannerReviewDocument
            {
                link = imagePath,
                status = 2,
                is_block_ads = isBlocked,
                cause_id = isBlocked ? 1 : 0,
                cause = note ?? "",
                take_time = now,
                dfp_time = now,
                iframe_id = iframeId,
                link_iframe = GamReviewSelectors.BuildLinkIframe(_gamSettings.NetworkCode, creativeId),
                creative_id = creativeId,
                url = "",
                process_time = now,
                bot_index = _gamSettings.BotIndex,
                is_review = 1,
                review_time = now,
                note = note,
                detect_info = JsonSerializer.Serialize(new
                {
                    verdict = note,
                    model = "gemini",
                    reviewed_at = now
                })
            };

            await _repository.InsertAsync(doc, cancellationToken);
            Console.WriteLine($"Đã lưu Mongo creative_id={creativeId}, is_block_ads={isBlocked}");
            return outcome is ReviewBannerOutcome.Success;
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(ReviewAndSaveAsync), ex, cancellationToken: cancellationToken);
            return false;
        }
    }

    private (bool isBlocked, string? note) MapOutcome(ReviewBannerOutcome outcome) =>
        outcome switch
        {
            ReviewBannerOutcome.Success success => (
                success.Verdict == BannerVerdictKind.Blocked,
                success.Label),
            ReviewBannerOutcome.InvalidResponse invalid => (false, $"InvalidResponse: {invalid.RawText}"),
            ReviewBannerOutcome.ApiError api => (false, $"ApiError: {api.Message}"),
            ReviewBannerOutcome.MissingApiKey => (false, "MissingApiKey"),
            ReviewBannerOutcome.FileNotFound notFound => (false, $"FileNotFound: {notFound.Path}"),
            _ => (false, "Unknown")
        };

    private static string SaveElementScreenshot(IWebElement element, string creativeId)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(dir);
        var safeName = string.Concat(creativeId.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (safeName.Length > 120)
            safeName = safeName[..120];

        var filePath = Path.Combine(dir, $"{safeName}.png");
        // GetScreenshot() chỉ có trên WebElement (không có trên IWebElement).
        if (element is not WebElement webElement)
            throw new InvalidOperationException("Không chụp được element — cần instance WebElement.");

        var screenshot = webElement.GetScreenshot();
        screenshot.SaveAsFile(filePath);
        return filePath;
    }

    private static string ReadCreativeId(IWebDriver driver)
    {
        try
        {
            var el = driver.FindElement(By.XPath(GamReviewSelectors.CreativeIdInLightbox));
            return el.Text.Trim();
        }
        catch
        {
            return "";
        }
    }

    private static string TryReadIframeId(IWebDriver driver)
    {
        try
        {
            var el = driver.FindElement(By.XPath(GamReviewSelectors.LightboxIframeId));
            return el.GetDomAttribute("id") ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool TryClickNextAd(IWebDriver driver)
    {
        try
        {
            var next = driver.FindElement(By.XPath(GamReviewSelectors.NextAdButton));
            var disabled = next.GetDomAttribute("aria-disabled");
            if (string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
                return false;

            next.Click();
            Thread.Sleep(500);
            return true;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
        catch (ElementNotInteractableException)
        {
            return false;
        }
    }

    private static WebDriverWait CreateWait(IWebDriver driver, int seconds) =>
        new(driver, TimeSpan.FromSeconds(Math.Max(5, seconds)));

    private static Func<IWebDriver, IWebElement> WaitUntilVisible(By by) =>
        driver =>
        {
            var element = driver.FindElement(by);
            return element.Displayed ? element : throw new NoSuchElementException("Element chưa visible.");
        };

}
