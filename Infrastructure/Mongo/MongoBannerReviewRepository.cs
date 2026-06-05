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

    public async Task InsertAsync(
        BannerReviewDocument document,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            Console.WriteLine("Mongo.Enabled=false — bỏ qua insert.");
            return;
        }

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}
