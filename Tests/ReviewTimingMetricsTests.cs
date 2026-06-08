using AdOpsAgenReviewBanner.Application;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class ReviewTimingMetricsTests
{
    [Fact]
    public void TotalFromBannerAppearToTelegram_SumsAllPhases()
    {
        var timing = new ReviewTimingMetrics(
            ImageRead: TimeSpan.FromMilliseconds(50),
            PolicyLoad: TimeSpan.FromMilliseconds(10),
            PromptBuild: TimeSpan.FromMilliseconds(5),
            LlmAnalyze: TimeSpan.FromSeconds(35),
            PreviewWait: TimeSpan.FromSeconds(3),
            ScreenshotCapture: TimeSpan.FromMilliseconds(200),
            TelegramNotify: TimeSpan.FromMilliseconds(800));

        Assert.Equal(39.065, timing.TotalFromBannerAppearToTelegram.TotalSeconds, 2);
    }

    [Fact]
    public void ToTelegramSummary_IncludesEndToEndLine()
    {
        var timing = new ReviewTimingMetrics(
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30),
            PreviewWait: TimeSpan.FromSeconds(3),
            ScreenshotCapture: TimeSpan.FromMilliseconds(150),
            TelegramNotify: TimeSpan.FromMilliseconds(500));

        var summary = timing.ToTelegramSummary();

        Assert.Contains("Tổng banner mới → Telegram", summary);
        Assert.Contains("Chờ preview: 3.00s", summary);
    }
}
