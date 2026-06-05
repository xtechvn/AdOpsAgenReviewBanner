namespace AdOpsAgenReviewBanner.Configuration;

public sealed class GamReviewSettings
{
    public string NetworkCode { get; set; } = "27973503";
    public string BotIndex { get; set; } = "bot_review|1";
    public int GridWaitSeconds { get; set; } = 45;
    public int LightboxWaitSeconds { get; set; } = 20;
    public int NextAdWaitSeconds { get; set; } = 15;
    /// <summary>Nghỉ giữa các lần gọi Gemini (tránh quota free tier).</summary>
    public int GeminiDelaySeconds { get; set; } = 12;
}
