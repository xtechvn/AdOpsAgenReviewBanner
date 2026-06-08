using System.Text.RegularExpressions;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>So khớp từ khóa block/review — hỗ trợ tiếng Việt (có/không dấu).</summary>
public sealed class BannerKeywordMatcher
{
    private static readonly Regex GamblingContextSignalRegex = new(
        @"\b(money|bonus|casino\w*|betting|wager|jackpot|poker|roulette|777|chips?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VietnameseBettingSignalRegex = new(
        @"\b(cuoc|thuong|the thao|nap tien|khuyen mai|\d+\s*%|\+\s*\d+k)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VietnameseSportsBetPhraseRegex = new(
        @"(cuoc\s*(the\s*thao|ngay)|the\s*thao\s*cuoc|ca\s*cuoc|ca\s*do)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PercentOrBonusAmountRegex = new(
        @"(\d+\s*%|\+\s*\d+\s*k\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IKeywordCatalogProvider _catalogProvider;

    public BannerKeywordMatcher(IKeywordCatalogProvider catalogProvider)
    {
        _catalogProvider = catalogProvider;
    }

    public async Task<IReadOnlyList<string>> FindBlockKeywordsAsync(
        string text,
        ReviewPolicy? policy,
        CancellationToken cancellationToken = default)
    {
        var catalog = await _catalogProvider.GetCatalogAsync(cancellationToken);
        var normalized = VietnameseTextNormalizer.NormalizeForMatch(text);
        var hits = new List<string>();

        hits.AddRange(FindPhrases(normalized, catalog.BlockPhrases));
        hits.AddRange(FindStems(normalized, catalog.BlockStems));
        hits.AddRange(FindWords(normalized, catalog.BlockWords));

        if (VietnameseSportsBetPhraseRegex.IsMatch(normalized))
            hits.Add("cược thể thao (vi)");

        if (policy is not null)
        {
            foreach (var category in policy.Categories)
            {
                foreach (var keyword in category.Keywords)
                {
                    if (string.IsNullOrWhiteSpace(keyword))
                        continue;

                    var key = VietnameseTextNormalizer.NormalizeForMatch(keyword);
                    if (key.Length <= 3)
                    {
                        if (FindWords(normalized, [key]).Any())
                            hits.Add(keyword);
                    }
                    else if (key.Contains(' ', StringComparison.Ordinal))
                    {
                        if (normalized.Contains(key, StringComparison.Ordinal))
                            hits.Add(keyword);
                    }
                    else
                    {
                        if (FindStems(normalized, [key]).Any() || FindWords(normalized, [key]).Any())
                            hits.Add(keyword);
                    }
                }
            }
        }

        return hits.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<string>> FindReviewKeywordsAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var catalog = await _catalogProvider.GetCatalogAsync(cancellationToken);
        var normalized = VietnameseTextNormalizer.NormalizeForMatch(text);

        var hits = new List<string>();
        hits.AddRange(FindPhrases(normalized, catalog.ReviewPhrases));
        hits.AddRange(FindStems(normalized, catalog.ReviewStems));
        hits.AddRange(FindWords(normalized, catalog.ReviewWords));
        return hits.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool HasGamblingContext(string text)
    {
        var normalized = VietnameseTextNormalizer.NormalizeForMatch(text);

        if (VietnameseSportsBetPhraseRegex.IsMatch(normalized))
            return true;

        var hasVnBetting = VietnameseBettingSignalRegex.IsMatch(normalized);
        var hasPercentOrK = PercentOrBonusAmountRegex.IsMatch(normalized);
        if (hasVnBetting && hasPercentOrK)
            return true;

        if (normalized.Contains("cuoc", StringComparison.Ordinal)
            && (normalized.Contains("the thao", StringComparison.Ordinal) || hasPercentOrK))
            return true;

        var hasGame = Regex.IsMatch(normalized, @"\b(games?|gaming)\b", RegexOptions.IgnoreCase);
        return hasGame && GamblingContextSignalRegex.IsMatch(normalized);
    }

    private static IEnumerable<string> FindPhrases(string text, IReadOnlyList<string> phrases)
    {
        foreach (var phrase in phrases)
        {
            var key = VietnameseTextNormalizer.NormalizeForMatch(phrase);
            if (!string.IsNullOrWhiteSpace(key) && text.Contains(key, StringComparison.Ordinal))
                yield return phrase;
        }
    }

    private static IEnumerable<string> FindStems(string text, IReadOnlyList<string> stems)
    {
        foreach (var stem in stems)
        {
            var key = VietnameseTextNormalizer.NormalizeForMatch(stem);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (Regex.IsMatch(text, $@"\b{Regex.Escape(key)}\w*\b", RegexOptions.IgnoreCase))
                yield return stem;
        }
    }

    private static IEnumerable<string> FindWords(string text, IReadOnlyList<string> words)
    {
        foreach (var word in words)
        {
            var key = VietnameseTextNormalizer.NormalizeForMatch(word);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (Regex.IsMatch(text, $@"\b{Regex.Escape(key)}\b", RegexOptions.IgnoreCase))
                yield return word;
        }
    }
}
