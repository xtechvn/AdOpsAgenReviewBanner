using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

public sealed class BannerModerationResultHolder : IBannerModerationResultHolder
{
    private BannerModerationResult? _lastResult;

    public void SetLastResult(BannerModerationResult result) => _lastResult = result;

    public BannerModerationResult? ConsumeLastResult()
    {
        var result = _lastResult;
        _lastResult = null;
        return result;
    }
}
