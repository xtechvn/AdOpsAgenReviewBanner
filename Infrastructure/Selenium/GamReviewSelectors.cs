namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// XPath GAM Ad Review Center — bám cấu trúc DOM preview-panel (tránh _ngcontent-*).
/// DOM mẫu: preview-panel → preview-main → creative-preview-container → creative-preview → iframe darc-ad-preview-div-*_preview_*
/// </summary>
internal static class GamReviewSelectors
{
    /// <summary>Root lưới listing — scope iframe grid (không lẫn preview-panel).</summary>
    public const string GridListingRoot =
        "//reviewable-grid[contains(@class,'reviewable-grid')]";

    /// <summary>Mỗi hàng banner trên listing (approval-card trong reviewable row).</summary>
    public const string GridReviewableRows =
        GridListingRoot + "//div[contains(@class,'reviewable') and @role='row']";

    /// <summary>Vùng click mở preview từ listing.</summary>
    public const string GridCardClickTarget =
        ".//div[contains(@class,'preview-container')]//creative-preview[@role='img']";

    /// <summary>Iframe preview trên listing — id darc-ad-preview-div-N_preview_c...</summary>
    public static readonly string[] GridPreviewIframeIdCandidates =
    [
        ".//iframe[contains(@id,'darc-ad-preview-div') and contains(@id,'_preview_')]",
        ".//div[contains(@class,'preview-container')]//iframe[@id and string-length(@id) > 0]"
    ];

    /// <summary>Card banner đầu tiên trên lưới danh sách (fallback).</summary>
    public const string FirstAdPreviewCard =
        "(" + GridReviewableRows + "//creative-preview[@role='img'])[1]";

    /// <summary>Root panel preview — dùng scope để không nhầm element ngoài panel.</summary>
    public const string PreviewPanelRoot = "//div[contains(@class,'preview-panel')]";

    /// <summary>
    /// Target chụp ảnh cho Florence — ưu tiên khung ad (with-borders), sau đó creative-preview trong panel.
    /// </summary>
    public static readonly string[] AdPreviewScreenshotCandidates =
    [
        PreviewPanelRoot + "//div[contains(@class,'creative-container') and contains(@class,'with-borders')]",
        PreviewPanelRoot + "//creative-preview[.//div[starts-with(@id,'darc-ad-preview-div')]]",
        PreviewPanelRoot + "//div[contains(@class,'creative-preview-container')]//creative-preview",
        PreviewPanelRoot + "//div[contains(@class,'ad-preview-service-container') and starts-with(@id,'darc-ad-preview-div')]",
        "//div[contains(@class,'preview-main')]//creative-preview[.//iframe[contains(@id,'darc-ad-preview-div')]]",
        "//creative-preview[contains(@style,'max-width') and .//iframe[contains(@id,'darc-ad-preview-div')]]",
        "//creative-preview[.//iframe[contains(@id,'darc-ad-preview-div')]]"
    ];

    public static readonly string[] LightboxCreativePreviewCandidates = AdPreviewScreenshotCandidates;

    /// <summary>Alias tương thích code cũ.</summary>
    public static readonly string LightboxCreativePreview = AdPreviewScreenshotCandidates[2];

    /// <summary>Creative ID trong metadata panel (Ad info).</summary>
    public static readonly string[] CreativeIdCandidates =
    [
        "//creative-metadata-panel//div[contains(@class,'creative-id')]",
        "//div[contains(@class,'data') and contains(@class,'creative-id')]",
        "//div[contains(@class,'creative-id') and contains(@aria-labelledby,'creative-id')]"
    ];

    public static readonly string CreativeIdInLightbox = CreativeIdCandidates[2];

    /// <summary>Iframe preview trong panel — id dạng darc-ad-preview-div-23_preview_c807872932501_v0_1_1_</summary>
    public static readonly string[] PreviewIframeIdCandidates =
    [
        PreviewPanelRoot + "//iframe[contains(@id,'darc-ad-preview-div') and contains(@id,'_preview_')]",
        PreviewPanelRoot + "//iframe[contains(@id,'darc-ad-preview-div')]",
        "//div[starts-with(@id,'darc-ad-preview-div')]//iframe",
        "//creative-preview//iframe[contains(@id,'darc-ad-preview-div') and contains(@id,'_preview_')]"
    ];

    public static readonly string LightboxIframeId = PreviewIframeIdCandidates[0];

    /// <summary>Nút chuyển banner tiếp theo trong preview-navigation.</summary>
    public const string NextAdButton =
        "//div[contains(@class,'preview-navigation')]//material-button[contains(@class,'right-arrow')][@aria-label='Display the next ad.']";

    /// <summary>Đóng lightbox ad detail — DOM GAM: button.close-button aria-label="Close ad detail".</summary>
    public static readonly string[] ClosePreviewPanelCandidates =
    [
        "//button[contains(@class,'close-button')][@aria-label='Close ad detail']",
        "//div[contains(@class,'left-side')]//button[@aria-label='Close ad detail']",
        PreviewPanelRoot + "//button[@aria-label='Close ad detail']",
        PreviewPanelRoot + "//material-button[@aria-label='Close']",
        "//material-button[@aria-label='Close'][ancestor::div[contains(@class,'preview')]]"
    ];

    /// <summary>
    /// Thanh phân trang listing GAM — DOM: pagination-bar → div.selected + material-button.next.
    /// aria-label next: "Go to the next page" (khác "Display the next ad." trong preview).
    /// </summary>
    public const string GridPaginationBar = "//pagination-bar[contains(@class,'pagination-bar')]";

    public static readonly string[] GridNextPageButtonCandidates =
    [
        GridPaginationBar + "//material-button[contains(@class,'next')][@aria-label='Go to the next page']",
        GridPaginationBar + "//material-button[@aria-label='Go to the next page']",
        "//material-button[@aria-label='Go to the next page'][not(ancestor::div[contains(@class,'preview-navigation')])]",
        "//material-button[@aria-label='Next page'][not(ancestor::div[contains(@class,'preview-navigation')])]"
    ];

    /// <summary>Text phân trang grid, ví dụ "201 - 250 of many".</summary>
    public static readonly string[] GridPaginationLabelCandidates =
    [
        GridPaginationBar + "//div[contains(@class,'selected')]",
        "//pagination-bar//div[contains(@class,'selected')]",
        "//div[contains(@class,'page-range')]",
        "//*[contains(@class,'range-label') and contains(text(),' of ')]"
    ];

    /// <summary>GAM loading spinner trong preview — banner chưa render khi còn visible.</summary>
    public static readonly string[] PreviewLoadingSpinnerCandidates =
    [
        PreviewPanelRoot + "//material-spinner",
        PreviewPanelRoot + "//material-spinner//div[contains(@class,'spinner')]",
        PreviewPanelRoot + "//div[contains(@class,'creative-preview')]//material-spinner",
        PreviewPanelRoot + "//div[contains(@class,'creative-preview-container')]//material-spinner",
        "//div[contains(@class,'preview-main')]//material-spinner"
    ];

    /// <summary>Iframe preview đã inject vào DOM (chờ script render xong).</summary>
    public const string PreviewIframeReady =
        PreviewPanelRoot + "//iframe[contains(@id,'darc-ad-preview-div') and contains(@id,'_preview_')]";

    /// <summary>GAM hiển thị khi preview trống / 1x1 — bỏ qua, không classify.</summary>
    public const string ReportMissingPreviewButton =
        PreviewPanelRoot + "//material-button[contains(@class,'report-this-ad-button')][@aria-label='Report missing preview']";

    public static string BuildLinkIframe(string networkCode, string creativeId) =>
        $"https://admanager.google.com/{networkCode}#creatives/ad_review_center/product=CONTENT&ecid={Uri.EscapeDataString(creativeId)}";
}
