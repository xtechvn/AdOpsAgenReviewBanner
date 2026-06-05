using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Infrastructure.Configuration;

/// <summary>Chuyển DTO appsettings sang model Domain — tránh leak BannerReviewSettings vào Application.</summary>
public static class ReviewPolicyMapper
{
    public static ReviewPolicy ToDomain(BannerReviewSettings settings)
    {
        var categories = settings.BlockedCategories
            .Select(c => new BlockedCategory(
                c.Name,
                c.Description,
                c.Keywords))
            .ToList();

        return new ReviewPolicy(
            settings.BlockedLabel.Trim(),
            settings.ReviewedLabel.Trim(),
            categories);
    }
}
