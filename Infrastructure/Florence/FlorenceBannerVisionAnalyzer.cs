using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>
/// Triển khai IBannerVisionAnalyzer bằng Florence-2 local (không tốn phí API).
/// Trả về nhãn Blocked/Reviewed để VerdictParser xử lý như Gemini.
/// </summary>
public sealed class FlorenceBannerVisionAnalyzer : IBannerVisionAnalyzer
{
    private readonly IBannerModerationScanner _scanner;
    private readonly IBannerModerationResultHolder _resultHolder;
    private readonly IReviewPolicyProvider _policyProvider;
    private readonly IOptionsMonitor<BannerReviewSettings> _bannerReviewSettings;
    private readonly ITelegramNotifier _telegram;

    public FlorenceBannerVisionAnalyzer(
        IBannerModerationScanner scanner,
        IBannerModerationResultHolder resultHolder,
        IReviewPolicyProvider policyProvider,
        IOptionsMonitor<BannerReviewSettings> bannerReviewSettings,
        ITelegramNotifier telegram)
    {
        _scanner = scanner;
        _resultHolder = resultHolder;
        _policyProvider = policyProvider;
        _bannerReviewSettings = bannerReviewSettings;
        _telegram = telegram;
    }

    public async Task<string?> AnalyzeAsync(
        BannerImage image,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await _policyProvider.GetPolicyAsync(cancellationToken);
            var moderation = await _scanner.ScanAsync(image, policy, cancellationToken);
            _resultHolder.SetLastResult(moderation);
            var labels = _bannerReviewSettings.CurrentValue;

            return moderation.Action switch
            {
                BannerModerationAction.Blocked => labels.BlockedLabel,
                BannerModerationAction.NeedsReview => labels.BlockedLabel,
                BannerModerationAction.Allowed => labels.ReviewedLabel,
                _ => labels.BlockedLabel
            };
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(AnalyzeAsync), ex, cancellationToken: cancellationToken);
            throw;
        }
    }
}
