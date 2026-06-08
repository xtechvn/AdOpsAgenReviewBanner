using System.Text.Json;
using Xunit;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Infrastructure.Florence;

namespace AdOpsAgenReviewBanner.Tests;

public class FlorenceDetectInfoBuilderTests
{
    [Fact]
    public void ToJson_IncludesFlorenceFields()
    {
        var moderation = new BannerModerationResult
        {
            Action = BannerModerationAction.Blocked,
            AiDescription = "Casino banner with slot machine",
            OcrText = "WIN BIG BONUS",
            MatchedKeywords = ["casino", "bonus"],
            Reason = "Phát hiện từ khóa cấm: casino, bonus"
        };

        var json = FlorenceDetectInfoBuilder.ToJson(moderation, "Blocked", 1717500000);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("florence2", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("Blocked", doc.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("Blocked", doc.RootElement.GetProperty("action").GetString());
        Assert.Equal("Casino banner with slot machine", doc.RootElement.GetProperty("ai_description").GetString());
        Assert.Equal("WIN BIG BONUS", doc.RootElement.GetProperty("ocr_text").GetString());
        Assert.Equal("Phát hiện từ khóa cấm: casino, bonus", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("matched_keywords").GetArrayLength());
    }
}
