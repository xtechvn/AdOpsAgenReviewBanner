using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Ánh xạ kết quả Florence tier 1 sang nhãn Blocked/Reviewed.</summary>
internal static class FlorenceVerdictMapper
{
    public static string ToLabel(BannerModerationAction action, BannerReviewSettings labels) =>
        action switch
        {
            BannerModerationAction.Allowed => labels.ReviewedLabel,
            _ => labels.BlockedLabel
        };
}
