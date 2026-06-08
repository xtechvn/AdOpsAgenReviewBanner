using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AdOpsAgenReviewBanner.Infrastructure.Mongo;

public sealed class MongoBannerReviewRepository : IBannerReviewRepository
{
    private readonly IMongoCollection<BannerReviewDocument> _collection;
    private readonly MongoSettings _settings;

    public MongoBannerReviewRepository(IOptions<MongoSettings> settings)
    {
        _settings = settings.Value;
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString)
            || string.IsNullOrWhiteSpace(_settings.Database)
            || string.IsNullOrWhiteSpace(_settings.CollectionBannerReview))
        {
            throw new InvalidOperationException(
                "Mongo:ConnectionString, Database, CollectionBannerReview phải được cấu hình.");
        }

        var client = new MongoClient(_settings.ConnectionString);
        var db = client.GetDatabase(_settings.Database);
        _collection = db.GetCollection<BannerReviewDocument>(_settings.CollectionBannerReview);
    }

    public async Task<bool> ExistsByCreativeIdAsync(
        string creativeId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(creativeId))
            return false;

        return await _collection
            .Find(x => x.creative_id == creativeId)
            .AnyAsync(cancellationToken);
    }

    public async Task<HashSet<string>> FindExistingIframeIdsAsync(
        IEnumerable<string> iframeIds,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return [];

        var ids = iframeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return [];

        var filter = Builders<BannerReviewDocument>.Filter.In(x => x.iframe_id, ids);
        var existing = await _collection
            .Find(filter)
            .Project(x => x.iframe_id)
            .ToListAsync(cancellationToken);

        return existing
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal)!;
    }

    public async Task<bool> InsertAsync(
        BannerReviewDocument document,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            Console.WriteLine("Mongo.Enabled=false — bỏ qua insert.");
            return false;
        }

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        Console.WriteLine(
            $"Mongo insert OK → {_settings.Database}.{_settings.CollectionBannerReview} | creative_id={document.creative_id}");
        return true;
    }

    public async Task<IReadOnlyList<BannerReviewDocument>> FindPendingGamReviewAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || maxCount <= 0)
            return Array.Empty<BannerReviewDocument>();

        var limit = Math.Clamp(maxCount, 1, 500);
        return await _collection
            .Find(x => x.is_review == 0 && x.creative_id != null && x.creative_id != "")
            .SortBy(x => x.process_time)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkReviewedByCreativeIdAsync(
        string creativeId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(creativeId))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<BannerReviewDocument>.Filter.Eq(x => x.creative_id, creativeId);
        var update = Builders<BannerReviewDocument>.Update
            .Set(x => x.is_review, 1)
            .Set(x => x.review_time, now);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        var ok = result.ModifiedCount > 0 || result.MatchedCount > 0;
        if (ok)
        {
            Console.WriteLine(
                $"Mongo update is_review=1 → creative_id={creativeId}, review_time={now}");
        }

        return ok;
    }
}
