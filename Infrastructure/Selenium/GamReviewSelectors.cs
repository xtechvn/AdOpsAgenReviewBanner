namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>XPath GAM Ad Review Center — tránh class _ngcontent-* (Angular đổi mỗi build).</summary>
internal static class GamReviewSelectors
{
    public const string FirstAdPreviewCard =
        "(//div[contains(@class,'reviewable') and @role='row']//creative-preview[@role='img'])[1]";

    public const string LightboxCreativePreview =
        "//creative-preview[contains(@style,'max-width') and .//iframe[contains(@id,'darc-ad-preview-div')]]";

    public const string CreativeIdInLightbox =
        "//div[contains(@class,'creative-id') and contains(@aria-labelledby,'creative-id')]";

    public const string LightboxIframeId =
        "//creative-preview//iframe[contains(@id,'darc-ad-preview-div') and contains(@id,'_preview_')]";

    public const string NextAdButton =
        "//material-button[contains(@class,'right-arrow') and @aria-label='Display the next ad.']";

    public static string BuildLinkIframe(string networkCode, string creativeId) =>
        $"https://admanager.google.com/{networkCode}#creatives/ad_review_center/product=CONTENT&ecid={Uri.EscapeDataString(creativeId)}";
}
