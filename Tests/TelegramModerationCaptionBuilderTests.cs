using Xunit;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Infrastructure.Telegram;

namespace AdOpsAgenReviewBanner.Tests;

public class TelegramModerationCaptionBuilderTests
{
    [Fact]
    public void BuildMonitorSection_IncludesAiDescriptionAndReason()
    {
        var moderation = new BannerModerationResult
        {
            Action = BannerModerationAction.Blocked,
            AiDescription = "Gambling advertisement",
            OcrText = "PLAY NOW",
            MatchedKeywords = ["gambling"],
            Reason = "Phát hiện từ khóa cấm: gambling"
        };

        var section = TelegramModerationCaptionBuilder.BuildMonitorSection(moderation);

        Assert.Contains("Mô tả AI", section);
        Assert.Contains("Gambling advertisement", section);
        Assert.Contains("OCR", section);
        Assert.Contains("PLAY NOW", section);
        Assert.Contains("Nhận định", section);
        Assert.Contains("gambling", section);
    }

    [Fact]
    public void BuildMonitorSection_WhenNull_ReturnsEmpty()
    {
        Assert.Equal("", TelegramModerationCaptionBuilder.BuildMonitorSection(null));
    }
}
