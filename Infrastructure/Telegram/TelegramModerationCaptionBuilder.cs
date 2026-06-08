using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Infrastructure.Telegram;

public static class TelegramModerationCaptionBuilder
{
    public static string BuildMonitorSection(BannerModerationResult? moderation)
    {
        if (moderation is null)
            return "";

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(moderation.AiDescription))
            lines.Add($"• Mô tả AI: `{Escape(moderation.AiDescription, 220)}`");

        if (!string.IsNullOrWhiteSpace(moderation.OcrText))
            lines.Add($"• OCR: `{Escape(moderation.OcrText, 180)}`");

        if (moderation.MatchedKeywords.Count > 0)
        {
            var keywords = string.Join(", ", moderation.MatchedKeywords.Take(6));
            lines.Add($"• Từ khóa: `{Escape(keywords, 160)}`");
        }

        if (!string.IsNullOrWhiteSpace(moderation.Reason))
            lines.Add($"• Nhận định: `{Escape(moderation.Reason, 180)}`");

        if (!string.IsNullOrWhiteSpace(moderation.ErrorMessage))
            lines.Add($"• Lỗi Florence: `{Escape(moderation.ErrorMessage, 120)}`");

        return lines.Count == 0 ? "" : string.Join("\n", lines) + "\n";
    }

    private static string Escape(string value, int maxLength)
    {
        var text = value.Trim();
        if (text.Length > maxLength)
            text = text[..maxLength] + "...";

        return text
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*");
    }
}
