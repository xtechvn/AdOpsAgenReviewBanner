using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Domain.Services;

/// <inheritdoc />
public sealed class VerdictParser : IVerdictParser
{
    public BannerVerdictKind? Parse(string? rawResponse, ReviewPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return null;

        var text = rawResponse.Trim();
        var blocked = policy.BlockedLabel;
        var reviewed = policy.ReviewedLabel;

        if (text.Equals(blocked, StringComparison.OrdinalIgnoreCase))
            return BannerVerdictKind.Blocked;

        if (text.Equals(reviewed, StringComparison.OrdinalIgnoreCase))
            return BannerVerdictKind.Reviewed;

        // Model đôi khi thêm ký tự thừa — fallback contains (ưu tiên Blocked nếu chỉ có blocked).
        if (text.Contains(blocked, StringComparison.OrdinalIgnoreCase)
            && !text.Contains(reviewed, StringComparison.OrdinalIgnoreCase))
            return BannerVerdictKind.Blocked;

        if (text.Contains(reviewed, StringComparison.OrdinalIgnoreCase))
            return BannerVerdictKind.Reviewed;

        return null;
    }
}
