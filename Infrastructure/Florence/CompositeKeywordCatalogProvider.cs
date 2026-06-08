using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Gộp builtin EN/VI + BlockedCategories appsettings + API (nếu bật).</summary>
public sealed class CompositeKeywordCatalogProvider : IKeywordCatalogProvider
{
    private readonly IOptionsMonitor<BannerReviewSettings> _bannerSettings;
    private readonly HttpKeywordCatalogProvider _apiProvider;
    private readonly object _cacheLock = new();
    private KeywordCatalog? _cached;
    private DateTime _cachedAtUtc;

    public CompositeKeywordCatalogProvider(
        IOptionsMonitor<BannerReviewSettings> bannerSettings,
        HttpKeywordCatalogProvider apiProvider)
    {
        _bannerSettings = bannerSettings;
        _apiProvider = apiProvider;
    }

    public async Task<KeywordCatalog> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var apiSettings = _apiProvider.Settings;
        var cacheMinutes = Math.Max(1, apiSettings.CacheMinutes);

        lock (_cacheLock)
        {
            if (_cached is not null && DateTime.UtcNow - _cachedAtUtc < TimeSpan.FromMinutes(cacheMinutes))
                return _cached;
        }

        var fromApi = apiSettings.Enabled
            ? await _apiProvider.FetchAsync(cancellationToken)
            : null;

        var catalog = Merge(
            BuiltinKeywordDefaults.BlockPhrases,
            BuiltinKeywordDefaults.BlockStems,
            BuiltinKeywordDefaults.BlockWords,
            BuiltinKeywordDefaults.ReviewPhrases,
            BuiltinKeywordDefaults.ReviewStems,
            BuiltinKeywordDefaults.ReviewWords,
            CollectPolicyKeywords(_bannerSettings.CurrentValue),
            fromApi);

        lock (_cacheLock)
        {
            _cached = catalog;
            _cachedAtUtc = DateTime.UtcNow;
        }

        return catalog;
    }

    public static KeywordCatalog CreateBuiltinCatalog() =>
        Merge(
            BuiltinKeywordDefaults.BlockPhrases,
            BuiltinKeywordDefaults.BlockStems,
            BuiltinKeywordDefaults.BlockWords,
            BuiltinKeywordDefaults.ReviewPhrases,
            BuiltinKeywordDefaults.ReviewStems,
            BuiltinKeywordDefaults.ReviewWords,
            [],
            null);

    public static KeywordCatalog Merge(
        IReadOnlyList<string> blockPhrases,
        IReadOnlyList<string> blockStems,
        IReadOnlyList<string> blockWords,
        IReadOnlyList<string> reviewPhrases,
        IReadOnlyList<string> reviewStems,
        IReadOnlyList<string> reviewWords,
        IReadOnlyList<string> policyBlockKeywords,
        KeywordCatalog? apiCatalog)
    {
        return new KeywordCatalog
        {
            BlockPhrases = DistinctMerge(blockPhrases, apiCatalog?.BlockPhrases),
            BlockStems = DistinctMerge(blockStems, apiCatalog?.BlockStems),
            BlockWords = DistinctMerge(blockWords, apiCatalog?.BlockWords, policyBlockKeywords),
            ReviewPhrases = DistinctMerge(reviewPhrases, apiCatalog?.ReviewPhrases),
            ReviewStems = DistinctMerge(reviewStems, apiCatalog?.ReviewStems),
            ReviewWords = DistinctMerge(reviewWords, apiCatalog?.ReviewWords)
        };
    }

    private static IReadOnlyList<string> CollectPolicyKeywords(BannerReviewSettings settings) =>
        settings.BlockedCategories
            .SelectMany(c => c.Keywords)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> DistinctMerge(params IReadOnlyList<string>?[] sources)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
                continue;

            foreach (var item in source)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    set.Add(item.Trim());
            }
        }

        return set.ToList();
    }
}
