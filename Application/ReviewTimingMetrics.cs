namespace AdOpsAgenReviewBanner.Application;

/// <summary>Chỉ số thời gian thực thi pipeline review banner.</summary>
public sealed record ReviewTimingMetrics(
    TimeSpan ImageRead,
    TimeSpan PolicyLoad,
    TimeSpan PromptBuild,
    TimeSpan LlmAnalyze)
{
    /// <summary>Từ bắt đầu đọc ảnh đến khi LLM trả về xong.</summary>
    public TimeSpan TotalFromImageReadToLlmDone => ImageRead + PolicyLoad + PromptBuild + LlmAnalyze;

    public string ToTelegramSummary() =>
        $"⏱ Đọc ảnh: {Format(ImageRead)} | LLM: {Format(LlmAnalyze)} | Tổng: {Format(TotalFromImageReadToLlmDone)}";

    private static string Format(TimeSpan duration) =>
        duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:F2}s"
            : $"{duration.TotalMilliseconds:F0}ms";
}
