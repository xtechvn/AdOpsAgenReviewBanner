namespace AdOpsAgenReviewBanner.Application.Queue;

/// <summary>Chuẩn hóa link_review GAM — fallback khi null/rỗng.</summary>
public static class GamReviewLinkResolver
{
    public const string DefaultAdReviewCenterUrl =
        "https://admanager.google.com/27973503#creatives/ad_review_center/product=CONTENT";

    public static string Resolve(string? linkReview, string? configuredDefault = null)
    {
        if (!string.IsNullOrWhiteSpace(linkReview))
            return linkReview.Trim();

        return string.IsNullOrWhiteSpace(configuredDefault)
            ? DefaultAdReviewCenterUrl
            : configuredDefault.Trim();
    }
}
