using System.Diagnostics;
using System.Text.Json;
using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Infrastructure.Florence;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// Reviewed mode: mở link_review → preview queue (nút "Display the next ad.") → Next page grid → lặp.
/// Trước khi mở preview: quét iframe id trên listing → so Mongo iframe_id → skip / next page.
/// Dừng khi không còn nút Next preview và không còn Next page grid. HashSet creative_id chống loop.
/// </summary>
public sealed class GamAdReviewCenterWorkflow : IGamAdReviewWorkflow
{
    private readonly ChromeDriverFactory _chromeDriverFactory;
    private readonly ReviewBannerUseCase _useCase;
    private readonly IBannerReviewRepository _repository;
    private readonly IExecutePlanQueuePublisher _executePlanQueuePublisher;
    private readonly GamGeneralAdCategoryFilter _categoryFilter;
    private readonly ITelegramNotifier _telegram;
    private readonly GamReviewSettings _gamSettings;

    public GamAdReviewCenterWorkflow(
        ChromeDriverFactory chromeDriverFactory,
        ReviewBannerUseCase useCase,
        IBannerReviewRepository repository,
        IExecutePlanQueuePublisher executePlanQueuePublisher,
        GamGeneralAdCategoryFilter categoryFilter,
        ITelegramNotifier telegram,
        IOptions<GamReviewSettings> gamSettings)
    {
        _chromeDriverFactory = chromeDriverFactory;
        _useCase = useCase;
        _repository = repository;
        _executePlanQueuePublisher = executePlanQueuePublisher;
        _categoryFilter = categoryFilter;
        _telegram = telegram;
        _gamSettings = gamSettings.Value;
    }

    public async Task<GamReviewWorkflowResult> ProcessReviewListAsync(
        string listUrl,
        int categoryOrder,
        string? categoryName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(listUrl))
            throw new ArgumentException("link_review không được rỗng.", nameof(listUrl));

        if (categoryOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(categoryOrder), categoryOrder, "order phải >= 1.");

        ChromeDriver? driver = null;
        var processed = 0;
        var skipped = 0;
        var skippedPreview = 0;
        var reviewed = 0;
        var errors = 0;
        var gridPages = 0;
        var seenCreativeIds = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            driver = _chromeDriverFactory.CreateResilient();
            driver.Navigate().GoToUrl(listUrl);

            var gridWait = CreateWait(driver, _gamSettings.GridWaitSeconds);
            _categoryFilter.Apply(driver, gridWait, categoryOrder, cancellationToken);
            var lightboxWait = CreateWait(driver, _gamSettings.LightboxWaitSeconds);
            var nextAdWait = CreateWait(driver, _gamSettings.NextAdWaitSeconds);

            while (!cancellationToken.IsCancellationRequested)
            {
                gridPages++;
                var pageLabel = GamGridPagination.TryReadPaginationLabel(driver);
                Console.WriteLine(
                    pageLabel is null
                        ? $"── Grid trang {gridPages} ──"
                        : $"── Grid trang {gridPages}: {pageLabel} ──");

                var gridOpen = await TryOpenFirstUnreviewedFromGridAsync(
                    driver,
                    gridWait,
                    lightboxWait,
                    cancellationToken);
                skipped += gridOpen.SkippedOnPage;

                if (gridOpen.Result == GridOpenResult.AllPageInMongo)
                {
                    if (!_gamSettings.EnableGridPagination)
                    {
                        Console.WriteLine("EnableGridPagination=false — dừng sau khi skip cả trang.");
                        break;
                    }

                    if (!TryAdvanceGridPage(driver, gridWait, out var allPageEndReason))
                    {
                        Console.WriteLine(allPageEndReason);
                        if (allPageEndReason.Contains("thất bại", StringComparison.OrdinalIgnoreCase)
                            || allPageEndReason.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
                            errors++;
                        break;
                    }

                    continue;
                }

                if (gridOpen.Result is GridOpenResult.NoCards or GridOpenResult.Failed)
                {
                    Console.Error.WriteLine(
                        gridOpen.Result == GridOpenResult.NoCards
                            ? "Không tìm thấy banner trên lưới — dừng job."
                            : "Không mở được preview từ lưới — dừng job.");
                    errors++;
                    break;
                }

                var queueStats = await RunPreviewQueueAsync(
                    driver,
                    seenCreativeIds,
                    nextAdWait,
                    listUrl,
                    categoryOrder,
                    categoryName,
                    cancellationToken);
                processed += queueStats.Processed;
                skipped += queueStats.Skipped;
                skippedPreview += queueStats.SkippedPreview;
                reviewed += queueStats.Reviewed;
                errors += queueStats.Errors;

                if (!GamGridPagination.TryCloseLightboxAndReturnToGrid(driver))
                {
                    Console.Error.WriteLine("Không đóng được lightbox / chưa về listing — dừng job.");
                    errors++;
                    break;
                }

                if (!_gamSettings.EnableGridPagination)
                {
                    Console.WriteLine("EnableGridPagination=false — dừng sau 1 lượt preview queue.");
                    break;
                }

                if (!TryAdvanceGridPage(driver, gridWait, out var endReason))
                {
                    Console.WriteLine(endReason);
                    if (endReason.Contains("thất bại", StringComparison.OrdinalIgnoreCase)
                        || endReason.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
                        errors++;
                    break;
                }

            }

            return new GamReviewWorkflowResult
            {
                ProcessedCount = processed,
                SkippedExistingCount = skipped,
                SkippedPreviewCount = skippedPreview,
                ReviewedCount = reviewed,
                ErrorCount = errors,
                GridPagesProcessed = gridPages
            };
        }
        finally
        {
            try { driver?.Quit(); } catch { }
            try { driver?.Dispose(); } catch { }
        }
    }

    private async Task<PreviewQueueStats> RunPreviewQueueAsync(
        ChromeDriver driver,
        HashSet<string> seenCreativeIds,
        WebDriverWait nextAdWait,
        string linkReview,
        int categoryOrder,
        string? categoryName,
        CancellationToken cancellationToken)
    {
        var stats = new PreviewQueueStats();
        string? previousCreativeId = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (previousCreativeId is not null)
                    WaitForNextCreativePreview(driver, nextAdWait, previousCreativeId);

                var creativeId = ReadCreativeId(driver);
                if (string.IsNullOrWhiteSpace(creativeId))
                {
                    Console.Error.WriteLine("Không đọc được creative_id — thử Next ad.");
                    stats.Errors++;
                    if (!TryAdvancePreviewOrStop(driver, "Không đọc được creative_id"))
                        break;
                    continue;
                }

                if (seenCreativeIds.Contains(creativeId))
                {
                    Console.WriteLine(
                        $"creative_id {creativeId} đã gặp trong phiên — hết preview queue (chống loop).");
                    break;
                }

                seenCreativeIds.Add(creativeId);
                previousCreativeId = creativeId;
                stats.Processed++;

                if (await _repository.ExistsByCreativeIdAsync(creativeId, cancellationToken))
                {
                    Console.WriteLine($"Skip creative_id đã tồn tại (bỏ qua chờ preview): {creativeId}");
                    stats.Skipped++;
                }
                else
                {
                    var bannerAppearWatch = Stopwatch.StartNew();
                    var loadState = GamPreviewLoadWatcher.WaitUntilSettled(driver, _gamSettings, creativeId);
                    var previewWait = bannerAppearWatch.Elapsed;

                    if (loadState is PreviewLoadState.Undersized
                        or PreviewLoadState.NoIframe
                        or PreviewLoadState.SpinnerTimeout)
                    {
                        var dims = GamPreviewInspector.TryGetRenderedPreviewDimensions(driver);
                        var skipReason = loadState switch
                        {
                            PreviewLoadState.Undersized => PreviewSkipReason.Undersized,
                            PreviewLoadState.SpinnerTimeout => PreviewSkipReason.SpinnerLoading,
                            _ => PreviewSkipReason.NoIframe
                        };
                        Console.WriteLine(GamPreviewInspector.FormatSkipMessage(skipReason, creativeId, dims, _gamSettings));
                        stats.SkippedPreview++;
                    }
                    else
                    {
                        var outcome = await ReviewAndSaveAsync(
                            driver,
                            creativeId,
                            previewWait,
                            linkReview,
                            categoryOrder,
                            categoryName,
                            cancellationToken);
                        if (outcome)
                            stats.Reviewed++;
                        else
                            stats.Errors++;

                        if (_gamSettings.GeminiDelaySeconds > 0)
                            await Task.Delay(TimeSpan.FromSeconds(_gamSettings.GeminiDelaySeconds), cancellationToken);
                    }
                }

                if (!TryAdvancePreviewOrStop(driver, "Hết banner trong preview queue"))
                    break;
            }
            catch (Exception ex)
            {
                stats.Errors++;
                Console.Error.WriteLine($"Lỗi xử lý banner: {ex.Message}");
                await _telegram.NotifyExceptionAsync(nameof(RunPreviewQueueAsync), ex, cancellationToken: cancellationToken);
                if (!TryAdvancePreviewOrStop(driver, "Lỗi xử lý banner"))
                    break;
            }
        }

        return stats;
    }

    private sealed class PreviewQueueStats
    {
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public int SkippedPreview { get; set; }
        public int Reviewed { get; set; }
        public int Errors { get; set; }
    }

    private enum GridOpenResult
    {
        Opened,
        AllPageInMongo,
        NoCards,
        Failed
    }

    private sealed record GridOpenAttempt(GridOpenResult Result, int SkippedOnPage);

    private async Task<GridOpenAttempt> TryOpenFirstUnreviewedFromGridAsync(
        ChromeDriver driver,
        WebDriverWait gridWait,
        WebDriverWait lightboxWait,
        CancellationToken cancellationToken)
    {
        if (GamGridPagination.IsPreviewPanelOpen(driver)
            && !string.IsNullOrWhiteSpace(ReadCreativeId(driver)))
            return new GridOpenAttempt(GridOpenResult.Opened, 0);

        if (GamGridPagination.IsPreviewPanelOpen(driver))
            GamGridPagination.TryCloseLightboxAndReturnToGrid(driver);

        try
        {
            gridWait.Until(WaitUntilVisible(By.XPath(GamReviewSelectors.GridReviewableRows)));
        }
        catch (WebDriverTimeoutException)
        {
            return new GridOpenAttempt(GridOpenResult.NoCards, 0);
        }

        var cards = GamGridListingScanner.ScanListingCards(driver);
        if (cards.Count == 0)
            return new GridOpenAttempt(GridOpenResult.NoCards, 0);

        var iframeIds = cards
            .Select(c => c.IframeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var existingInMongo = await _repository.FindExistingIframeIdsAsync(iframeIds, cancellationToken);

        var skippedOnPage = 0;
        foreach (var card in cards)
        {
            if (!string.IsNullOrWhiteSpace(card.IframeId)
                && existingInMongo.Contains(card.IframeId))
            {
                skippedOnPage++;
                Console.WriteLine($"[Grid] Skip iframe đã có Mongo: {card.IframeId}");
                continue;
            }

            var label = string.IsNullOrWhiteSpace(card.IframeId)
                ? "(chưa có iframe — card đang load)"
                : card.IframeId;
            Console.WriteLine($"[Grid] Mở banner mới: iframe_id={label}");

            try
            {
                card.ClickTarget.Click();
                WaitForPreviewPanelReady(driver, lightboxWait);
                return new GridOpenAttempt(GridOpenResult.Opened, skippedOnPage);
            }
            catch (ElementClickInterceptedException)
            {
                try
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", card.ClickTarget);
                    WaitForPreviewPanelReady(driver, lightboxWait);
                    return new GridOpenAttempt(GridOpenResult.Opened, skippedOnPage);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Grid] Click card thất bại: {ex.Message}");
                    return new GridOpenAttempt(GridOpenResult.Failed, skippedOnPage);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Grid] Click card thất bại: {ex.Message}");
                return new GridOpenAttempt(GridOpenResult.Failed, skippedOnPage);
            }
        }

        Console.WriteLine($"[Grid] Cả {cards.Count} banner trên trang đã có trong Mongo — next page.");
        return new GridOpenAttempt(GridOpenResult.AllPageInMongo, skippedOnPage);
    }

    private static bool TryAdvanceGridPage(IWebDriver driver, WebDriverWait gridWait, out string reason)
    {
        var gridNextState = GamGridPagination.GetGridNextPageState(driver);
        switch (gridNextState)
        {
            case GridNextPageState.Disabled:
                reason = "[Grid] Next page disabled — hết banner toàn bộ listing, hoàn tất job.";
                return false;
            case GridNextPageState.NotFound:
                reason = "[Grid] Không tìm thấy nút Next page — dừng job.";
                return false;
            case GridNextPageState.Enabled:
                if (!GamGridPagination.TryClickGridNextPage(driver))
                {
                    reason = "[Grid] Next page enabled nhưng click thất bại — dừng job.";
                    return false;
                }

                gridWait.Until(WaitUntilVisible(By.XPath(GamReviewSelectors.GridReviewableRows)));
                reason = "";
                return true;
            default:
                reason = "[Grid] Trạng thái phân trang không xác định — dừng job.";
                return false;
        }
    }

    private static bool TryAdvancePreviewOrStop(IWebDriver driver, string stopReason)
    {
        if (IsNextAdAvailable(driver) && TryClickNextAd(driver))
            return true;

        Console.WriteLine($"{stopReason} — nút 'Display the next ad.' không còn hoặc disabled.");
        return false;
    }

    private static bool IsNextAdAvailable(IWebDriver driver)
    {
        try
        {
            var next = driver.FindElement(By.XPath(GamReviewSelectors.NextAdButton));
            if (!next.Displayed)
                return false;

            var disabled = next.GetDomAttribute("aria-disabled");
            return !string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (NoSuchElementException)
        {
            return false;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    private async Task<bool> ReviewAndSaveAsync(
        ChromeDriver driver,
        string creativeId,
        TimeSpan previewWait,
        string linkReview,
        int categoryOrder,
        string? categoryName,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = FindVisibleAdPreviewTarget(driver)
                ?? throw new NoSuchElementException("Không tìm thấy vùng preview để chụp ảnh.");

            var captureWatch = Stopwatch.StartNew();
            var imagePath = await CapturePreviewWithSpinnerRetryAsync(preview, creativeId, cancellationToken);
            captureWatch.Stop();

            Console.WriteLine("⏱ [Bước 3] Bắt đầu phân tích Florence-2...");
            var outcome = await _useCase.ExecuteAsync(
                imagePath,
                cancellationToken,
                previewWait,
                captureWatch.Elapsed);
            Console.WriteLine($"⏱ [Bước 3] Xong — {MapOutcomeLabel(outcome)}");
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
                is_review = 0,
                review_time = 0,
                note = note,
                category_name = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim(),
                user_name = "n8n",
                detect_info = BuildDetectInfo(outcome, note, now)
            };

            var inserted = await _repository.InsertAsync(doc, cancellationToken);
            if (inserted)
            {
                await _executePlanQueuePublisher.PublishAfterMongoInsertAsync(
                    new ExecutePlanPublishRequest
                    {
                        CreativeId = creativeId,
                        IsBlockAds = isBlocked,
                        LinkReview = linkReview,
                        Order = categoryOrder,
                        Category = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim()
                    },
                    cancellationToken);
            }

            Console.WriteLine($"Đã lưu Mongo creative_id={creativeId}, is_block_ads={isBlocked}");
            return outcome is ReviewBannerOutcome.Success;
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(ReviewAndSaveAsync), ex, cancellationToken: cancellationToken);
            return false;
        }
    }

    private static string BuildDetectInfo(ReviewBannerOutcome outcome, string? note, long reviewedAtUnix) =>
        outcome is ReviewBannerOutcome.Success { Moderation: { } moderation } success
            ? FlorenceDetectInfoBuilder.ToJson(moderation, success.Label, reviewedAtUnix, success.Timing)
            : JsonSerializer.Serialize(new
            {
                verdict = note,
                model = "florence2",
                reviewed_at = reviewedAtUnix
            });

    private static string MapOutcomeLabel(ReviewBannerOutcome outcome) =>
        outcome switch
        {
            ReviewBannerOutcome.Success success => success.Label,
            ReviewBannerOutcome.InvalidResponse invalid => $"Invalid: {invalid.RawText}",
            ReviewBannerOutcome.ApiError api => $"Error: {api.Message}",
            ReviewBannerOutcome.FileNotFound notFound => $"FileNotFound: {notFound.Path}",
            ReviewBannerOutcome.MissingApiKey => "MissingApiKey",
            _ => outcome.GetType().Name
        };

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

    private async Task<string> CapturePreviewWithSpinnerRetryAsync(
        IWebElement preview,
        string creativeId,
        CancellationToken cancellationToken)
    {
        var screenshotWatch = Stopwatch.StartNew();
        var imagePath = SaveElementScreenshot(preview, creativeId);
        screenshotWatch.Stop();
        Console.WriteLine(
            $"⏱ [Bước 2] Chụp ảnh: {screenshotWatch.ElapsedMilliseconds}ms → {Path.GetFileName(imagePath)}");

        if (!BannerTopLeftSpinnerDetector.HasLoadingSpinner(imagePath, _gamSettings))
            return imagePath;

        var retrySec = Math.Max(1, _gamSettings.PreviewScreenshotRetryDelaySeconds);
        Console.WriteLine(
            $"⏱ [Bước 2] Phát hiện icon loading góc trên-trái — delay {retrySec}s rồi chụp lại...");
        await Task.Delay(TimeSpan.FromSeconds(retrySec), cancellationToken);

        screenshotWatch.Restart();
        imagePath = SaveElementScreenshot(preview, creativeId);
        screenshotWatch.Stop();
        Console.WriteLine(
            $"⏱ [Bước 2] Chụp lại: {screenshotWatch.ElapsedMilliseconds}ms → {Path.GetFileName(imagePath)}");

        return imagePath;
    }

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
        foreach (var xpath in GamReviewSelectors.CreativeIdCandidates)
        {
            try
            {
                var el = driver.FindElement(By.XPath(xpath));
                var text = el.Text.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            catch (NoSuchElementException)
            {
            }
        }

        return "";
    }

    private static string TryReadIframeId(IWebDriver driver)
    {
        foreach (var xpath in GamReviewSelectors.PreviewIframeIdCandidates)
        {
            try
            {
                var el = driver.FindElement(By.XPath(xpath));
                return el.GetDomAttribute("id") ?? "";
            }
            catch (NoSuchElementException)
            {
            }
        }

        return "";
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

    private static void WaitForPreviewPanelReady(IWebDriver driver, WebDriverWait wait) =>
        wait.Until(d =>
        {
            if (!string.IsNullOrWhiteSpace(ReadCreativeId(d)))
                return true;

            try
            {
                var panel = d.FindElement(By.XPath(GamReviewSelectors.PreviewPanelRoot));
                if (panel.Displayed)
                    return true;
            }
            catch (NoSuchElementException)
            {
            }

            throw new NoSuchElementException("Preview panel chưa sẵn sàng.");
        });

    private static IWebElement WaitForAdPreviewScreenshotTarget(IWebDriver driver, WebDriverWait wait) =>
        wait.Until(d =>
        {
            WaitForPreviewIframe(d, TimeSpan.FromSeconds(2));
            var target = FindVisibleAdPreviewTarget(d);
            if (target is null)
                throw new NoSuchElementException("Không tìm thấy vùng preview banner trong preview-panel.");

            return target;
        });

    private static void WaitForNextCreativePreview(IWebDriver driver, WebDriverWait wait, string previousCreativeId) =>
        wait.Until(d =>
        {
            var id = ReadCreativeId(d);
            if (string.IsNullOrWhiteSpace(id) || id == previousCreativeId)
                throw new NoSuchElementException("Chưa chuyển sang creative tiếp theo.");

            return true;
        });

    private static void WaitForPreviewIframe(IWebDriver driver, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var iframe = driver.FindElement(By.XPath(GamReviewSelectors.PreviewIframeReady));
                if (iframe.Displayed)
                    return;
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }

            Thread.Sleep(200);
        }
    }

    private static IWebElement? FindVisibleAdPreviewTarget(IWebDriver driver)
    {
        foreach (var xpath in GamReviewSelectors.AdPreviewScreenshotCandidates)
        {
            try
            {
                var element = driver.FindElement(By.XPath(xpath));
                if (element.Displayed && element.Size.Width >= 20 && element.Size.Height >= 20)
                    return element;
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return null;
    }

}
