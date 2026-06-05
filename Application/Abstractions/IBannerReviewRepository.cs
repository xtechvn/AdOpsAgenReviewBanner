using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

public interface IBannerReviewRepository
{
    Task<bool> ExistsByCreativeIdAsync(string creativeId, CancellationToken cancellationToken = default);
    Task InsertAsync(BannerReviewDocument document, CancellationToken cancellationToken = default);
}
