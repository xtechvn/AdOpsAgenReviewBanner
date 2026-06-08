namespace AdOpsAgenReviewBanner.Application;

/// <summary>Chỉ số thời gian thực thi pipeline review banner.</summary>
public sealed record ReviewTimingMetrics(
    TimeSpan ImageRead,
    TimeSpan PolicyLoad,
    TimeSpan PromptBuild,
    TimeSpan LlmAnalyze,
    TimeSpan PreviewWait = default,
    TimeSpan ScreenshotCapture = default,
    TimeSpan TelegramNotify = default)
{
    /// <summary>Từ bắt đầu đọc ảnh đến khi LLM trả về xong.</summary>
    public TimeSpan TotalFromImageReadToLlmDone => ImageRead + PolicyLoad + PromptBuild + LlmAnalyze;

    /// <summary>Từ lúc banner mới xuất hiện (chờ preview) đến khi gửi Telegram xong.</summary>
    public TimeSpan TotalFromBannerAppearToTelegram =>
        PreviewWait + ScreenshotCapture + TotalFromImageReadToLlmDone + TelegramNotify;

    public string ToTelegramSummary() =>
        $"⏱ Chờ preview: {Format(PreviewWait)} | Chụp: {Format(ScreenshotCapture)} | " +
        $"Florence: {Format(LlmAnalyze)} | Gửi Telegram: {Format(TelegramNotify)}\n" +
        $"⏱ *Tổng banner mới → Telegram: {Format(TotalFromBannerAppearToTelegram)}*";

    private static string Format(TimeSpan duration) =>
        duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:F2}s"
            : $"{duration.TotalMilliseconds:F0}ms";
}
