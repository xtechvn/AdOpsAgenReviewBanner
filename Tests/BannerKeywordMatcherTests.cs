using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Infrastructure.Florence;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class BannerKeywordMatcherTests
{
    [Fact]
    public async Task FindBlockKeywords_MatchesVietnameseSportsBetting_WithDiacritics()
    {
        var matcher = CreateMatcher();
        var text = "CƯỢC THỂ THAO THƯỞNG 100% CƯỢC NGAY KINGG9";

        var hits = await matcher.FindBlockKeywordsAsync(text, null);

        Assert.Contains(hits, h => h.Contains("cược", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindBlockKeywords_MatchesVietnameseSportsBetting_WithoutDiacritics()
    {
        var matcher = CreateMatcher();
        var text = "CUOC THE THAO THUONG 100% CUOC NGAY";

        var hits = await matcher.FindBlockKeywordsAsync(text, null);

        Assert.NotEmpty(hits);
    }

    [Fact]
    public void HasGamblingContext_DetectsVietnameseBonusPattern()
    {
        var matcher = CreateMatcher();
        Assert.True(matcher.HasGamblingContext("cuoc the thao thuong 100% + 1500k"));
    }

    [Fact]
    public async Task FindBlockKeywords_StillMatchesEnglishSportsBetting()
    {
        var matcher = CreateMatcher();
        var hits = await matcher.FindBlockKeywordsAsync("Join our sports betting bonus today", null);

        Assert.Contains(hits, h => h.Equals("sports betting", StringComparison.OrdinalIgnoreCase));
    }

    private static BannerKeywordMatcher CreateMatcher()
    {
        var provider = new StaticKeywordCatalogProvider();
        return new BannerKeywordMatcher(provider);
    }

    private sealed class StaticKeywordCatalogProvider : IKeywordCatalogProvider
    {
        public Task<KeywordCatalog> GetCatalogAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CompositeKeywordCatalogProvider.CreateBuiltinCatalog());
    }
}
