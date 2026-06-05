using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Configuration;

/// <summary>Đọc policy từ appsettings.json.</summary>
public sealed class AppSettingsPolicyProvider : IReviewPolicyProvider
{
    private readonly IOptionsMonitor<BannerReviewSettings> _settings;
    private readonly ITelegramNotifier _telegram;

    public AppSettingsPolicyProvider(
        IOptionsMonitor<BannerReviewSettings> settings,
        ITelegramNotifier telegram)
    {
        _settings = settings;
        _telegram = telegram;
    }

    public async Task<ReviewPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = ReviewPolicyMapper.ToDomain(_settings.CurrentValue);
            return await Task.FromResult(policy);
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(GetPolicyAsync), ex, cancellationToken: cancellationToken);
            throw;
        }
    }
}
