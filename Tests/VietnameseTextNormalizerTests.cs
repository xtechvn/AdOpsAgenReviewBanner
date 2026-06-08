using AdOpsAgenReviewBanner.Infrastructure.Florence;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class VietnameseTextNormalizerTests
{
    [Fact]
    public void NormalizeForMatch_RemovesDiacritics()
    {
        var normalized = VietnameseTextNormalizer.NormalizeForMatch("CƯỢC THỂ THAO");

        Assert.Equal("cuoc the thao", normalized);
    }

    [Fact]
    public void NormalizeForMatch_LowercasesEnglish()
    {
        Assert.Equal("sports betting", VietnameseTextNormalizer.NormalizeForMatch("Sports Betting"));
    }
}
