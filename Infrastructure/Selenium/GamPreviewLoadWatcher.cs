using System.Diagnostics;

using AdOpsAgenReviewBanner.Configuration;

using OpenQA.Selenium;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

internal enum PreviewLoadState
{
    Ready,
    Undersized,
    Missing,
    NoIframe,
    /// <summary>Giữ enum tương thích — logic mới không timeout spinner DOM.</summary>
    SpinnerTimeout
}

/// <summary>Delay cố định sau banner mới rồi chuyển chụp — không poll material-spinner DOM.</summary>
internal static class GamPreviewLoadWatcher
{
    public static PreviewLoadState WaitUntilSettled(IWebDriver driver, GamReviewSettings settings, string? creativeId = null)
    {
        _ = creativeId;

        var watch = Stopwatch.StartNew();
        var initialSec = Math.Max(1, settings.PreviewInitialDelaySeconds);

        Console.WriteLine($"⏱ [Bước 1] Chờ preview {initialSec}s (banner mới)...");
        Thread.Sleep(TimeSpan.FromSeconds(initialSec));

        watch.Stop();

        var dims = GamPreviewInspector.TryGetIframeOnlyDimensions(driver)
            ?? GamPreviewInspector.TryGetRenderedPreviewDimensions(driver);

        if (dims is null)
        {
            Console.WriteLine(
                $"⏱ [Bước 1] Xong chờ preview — chuyển chụp ({watch.Elapsed.TotalSeconds:F1}s)");
            return PreviewLoadState.Ready;
        }

        if (dims.Value.Width < settings.MinPreviewWidth || dims.Value.Height < settings.MinPreviewHeight)
        {
            Console.WriteLine(
                $"⏱ [Bước 1] Xong chờ preview ({watch.Elapsed.TotalSeconds:F1}s) — " +
                $"{dims.Value.Width}x{dims.Value.Height} ({dims.Value.Source}) quá nhỏ");
            return PreviewLoadState.Undersized;
        }

        Console.WriteLine(
            $"⏱ [Bước 1] Xong chờ preview — chuyển chụp ({watch.Elapsed.TotalSeconds:F1}s, " +
            $"{dims.Value.Width}x{dims.Value.Height}, {dims.Value.Source})");
        return PreviewLoadState.Ready;
    }
}
