using System.Text.Json;
using System.Text.Json.Serialization;
using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Chuẩn hóa JSON lưu vào Mongo field detect_info.</summary>
public static class FlorenceDetectInfoBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static string ToJson(
        BannerModerationResult moderation,
        string verdict,
        long reviewedAtUnix,
        ReviewTimingMetrics? timing = null)
    {
        var payload = new FlorenceDetectInfoDto
        {
            Model = moderation.GeminiAttempted ? "florence2+gemini" : "florence2",
            Verdict = verdict,
            ReviewedAt = reviewedAtUnix,
            Action = moderation.Action.ToString(),
            FlorenceAction = moderation.FlorenceAction.ToString(),
            FinalSource = moderation.FinalSource,
            GeminiVerdict = moderation.GeminiVerdict,
            GeminiError = moderation.GeminiError,
            AiDescription = moderation.AiDescription,
            OcrText = moderation.OcrText,
            MatchedKeywords = moderation.MatchedKeywords.ToList(),
            Reason = moderation.Reason,
            Error = moderation.ErrorMessage,
            TimingSeconds = timing is null
                ? null
                : new FlorenceTimingDto
                {
                    ImageRead = timing.ImageRead.TotalSeconds,
                    FlorenceAnalyze = timing.LlmAnalyze.TotalSeconds,
                    Total = timing.TotalFromImageReadToLlmDone.TotalSeconds,
                    PreviewWait = timing.PreviewWait.TotalSeconds,
                    ScreenshotCapture = timing.ScreenshotCapture.TotalSeconds,
                    TelegramNotify = timing.TelegramNotify.TotalSeconds,
                    BannerAppearToTelegram = timing.TotalFromBannerAppearToTelegram.TotalSeconds
                }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private sealed class FlorenceDetectInfoDto
    {
        public string Model { get; set; } = "";
        public string Verdict { get; set; } = "";
        public long ReviewedAt { get; set; }
        public string Action { get; set; } = "";
        public string FlorenceAction { get; set; } = "";
        public string FinalSource { get; set; } = "";
        public string? GeminiVerdict { get; set; }
        public string? GeminiError { get; set; }
        public string AiDescription { get; set; } = "";
        public string OcrText { get; set; } = "";
        public List<string> MatchedKeywords { get; set; } = [];
        public string Reason { get; set; } = "";
        public string? Error { get; set; }
        public FlorenceTimingDto? TimingSeconds { get; set; }
    }

    private sealed class FlorenceTimingDto
    {
        public double ImageRead { get; set; }
        public double FlorenceAnalyze { get; set; }
        public double Total { get; set; }
        public double PreviewWait { get; set; }
        public double ScreenshotCapture { get; set; }
        public double TelegramNotify { get; set; }
        public double BannerAppearToTelegram { get; set; }
    }
}
