using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Quét banner bằng vision local (Florence-2) + rule từ khóa.</summary>
public interface IBannerModerationScanner
{
    Task<BannerModerationResult> ScanAsync(
        BannerImage image,
        ReviewPolicy policy,
        CancellationToken cancellationToken = default);
}
