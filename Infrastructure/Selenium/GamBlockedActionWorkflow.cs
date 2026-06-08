using System.Diagnostics;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// Blocked mode: Ad review center (Unreviewed) → search Creative ID → Select all → Allow/Block.
/// </summary>
public sealed class GamBlockedActionWorkflow : IGamBlockedActionWorkflow
{
    private readonly ChromeDriverFactory _chromeDriverFactory;
    private readonly ITelegramNotifier _telegram;
    private readonly GamReviewSettings _gamSettings;

    public GamBlockedActionWorkflow(
        ChromeDriverFactory chromeDriverFactory,
        ITelegramNotifier telegram,
        IOptions<GamReviewSettings> gamSettings)
    {
        _chromeDriverFactory = chromeDriverFactory;
        _telegram = telegram;
        _gamSettings = gamSettings.Value;
    }

    public async Task<GamBlockedActionResult> ApplyActionAsync(
        string creativeId,
        GamModerationAction action,
        string? linkReview = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(creativeId))
            return Fail(creativeId, action, "creative_id rỗng.");

        var actionLabel = action == GamModerationAction.Block ? "Blocked" : "Reviewed";
        ChromeDriver? driver = null;
        var watch = Stopwatch.StartNew();

        try
        {
            driver = _chromeDriverFactory.CreateResilient();
            var listUrl = GamReviewLinkResolver.Resolve(linkReview, _gamSettings.DefaultAdReviewCenterUrl);
            Console.WriteLine($"[Blocked] Mở Ad review center: {listUrl}");
            driver.Navigate().GoToUrl(listUrl);

            var wait = CreateWait(driver, _gamSettings.GridWaitSeconds);
            FilterByCreativeId(driver, wait, creativeId, cancellationToken);
            ClickSelectAllAndApplyAction(driver, wait, action, cancellationToken);

            watch.Stop();
            var message =
                $"✅ *GAM action xong*\n" +
                $"• creative_id: `{creativeId}`\n" +
                $"• action: *{actionLabel}*\n" +
                $"• thời gian: {watch.Elapsed.TotalSeconds:F1}s";
            await _telegram.NotifyBlockedActionAsync(message, cancellationToken);

            Console.WriteLine(
                $"[Blocked] Hoàn tất {actionLabel} creative_id={creativeId} ({watch.Elapsed.TotalSeconds:F1}s)");
            return new GamBlockedActionResult
            {
                Success = true,
                CreativeId = creativeId,
                ActionLabel = actionLabel
            };
        }
        catch (Exception ex)
        {
            watch.Stop();
            Console.Error.WriteLine($"[Blocked] Lỗi creative_id={creativeId}: {ex.Message}");
            await _telegram.NotifyExceptionAsync(nameof(ApplyActionAsync), ex, cancellationToken: cancellationToken);
            return Fail(creativeId, action, ex.Message);
        }
        finally
        {
            try { driver?.Quit(); } catch { }
            try { driver?.Dispose(); } catch { }
        }
    }

    private void FilterByCreativeId(
        IWebDriver driver,
        WebDriverWait wait,
        string creativeId,
        CancellationToken cancellationToken)
    {
        var searchInput = wait.Until(WaitUntilVisible(By.XPath(GamBlockedReviewSelectors.FilterSearchInput)));
        searchInput.Click();
        Thread.Sleep(300);

        var creativeMenuItem = WaitUntilAnyVisible(driver, wait, GamBlockedReviewSelectors.CreativeIdMenuItemCandidates);
        creativeMenuItem.Click();
        Thread.Sleep(300);

        var valueInput = WaitUntilAnyVisible(driver, wait, GamBlockedReviewSelectors.CreativeIdValueInputCandidates);
        valueInput.Clear();
        valueInput.SendKeys(creativeId);

        var applyButton = wait.Until(WaitUntilVisible(By.XPath(GamBlockedReviewSelectors.CreativeIdApplyButton)));
        applyButton.Click();

        var delaySec = Math.Max(1, _gamSettings.BlockedFilterApplyDelaySeconds);
        Console.WriteLine($"[Blocked] Đã Apply filter Creative ID — chờ {delaySec}s...");
        Thread.Sleep(TimeSpan.FromSeconds(delaySec));
        cancellationToken.ThrowIfCancellationRequested();

        WaitUntilAnyVisible(driver, wait, GamBlockedReviewSelectors.GridAdCardCandidates);
        Console.WriteLine("[Blocked] Kết quả lọc đã hiển thị.");
    }

    private void ClickSelectAllAndApplyAction(
        IWebDriver driver,
        WebDriverWait wait,
        GamModerationAction action,
        CancellationToken cancellationToken)
    {
        var selectAll = wait.Until(WaitUntilVisible(By.XPath(GamBlockedReviewSelectors.SelectAllAdsButton)));
        selectAll.Click();
        Thread.Sleep(500);
        cancellationToken.ThrowIfCancellationRequested();

        var actionXPath = action == GamModerationAction.Block
            ? GamBlockedReviewSelectors.BlockButton
            : GamBlockedReviewSelectors.AllowButton;
        var actionLabel = action == GamModerationAction.Block ? "Block" : "Allow";

        var actionButton = wait.Until(WaitUntilVisible(By.XPath(actionXPath)));
        actionButton.Click();
        Console.WriteLine($"[Blocked] Đã click {actionLabel}.");
        Thread.Sleep(1000);
    }

    private static IWebElement WaitUntilAnyVisible(IWebDriver driver, WebDriverWait wait, string[] xpaths)
    {
        var deadline = DateTime.UtcNow + wait.Timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var xpath in xpaths)
            {
                try
                {
                    var element = driver.FindElement(By.XPath(xpath));
                    if (element.Displayed)
                        return element;
                }
                catch (NoSuchElementException)
                {
                }
                catch (StaleElementReferenceException)
                {
                }
            }

            Thread.Sleep(200);
        }

        throw new NoSuchElementException($"Không tìm thấy element visible: {string.Join(" | ", xpaths)}");
    }

    private static GamBlockedActionResult Fail(string creativeId, GamModerationAction action, string error) =>
        new()
        {
            Success = false,
            CreativeId = creativeId,
            ActionLabel = action == GamModerationAction.Block ? "Blocked" : "Reviewed",
            ErrorMessage = error
        };

    private static WebDriverWait CreateWait(IWebDriver driver, int seconds) =>
        new(driver, TimeSpan.FromSeconds(Math.Max(10, seconds)));

    private static Func<IWebDriver, IWebElement> WaitUntilVisible(By by) =>
        d =>
        {
            var element = d.FindElement(by);
            return element.Displayed ? element : throw new NoSuchElementException("Element chưa visible.");
        };
}
