namespace AdOpsAgenReviewBanner.Configuration;

public sealed class GamReviewSettings
{
    public string NetworkCode { get; set; } = "27973503";
    public string BotIndex { get; set; } = "bot_review|1";
    public int GridWaitSeconds { get; set; } = 45;
    public int LightboxWaitSeconds { get; set; } = 20;
    public int NextAdWaitSeconds { get; set; } = 15;
    /// <summary>Delay cố định sau khi banner mới hiện trước khi chụp (giây).</summary>
    public int PreviewInitialDelaySeconds { get; set; } = 3;
    /// <summary>Delay trước khi chụp lại khi ảnh có icon loading góc trên-trái (giây).</summary>
    public int PreviewScreenshotRetryDelaySeconds { get; set; } = 3;
    /// <summary>Vùng quét góc trên-trái — tỷ lệ chiều rộng ảnh (0.25 = 25%).</summary>
    public double PreviewSpinnerRegionWidthRatio { get; set; } = 0.25;
    /// <summary>Vùng quét góc trên-trái — tỷ lệ chiều cao ảnh.</summary>
    public double PreviewSpinnerRegionHeightRatio { get; set; } = 0.25;
    /// <summary>Giới hạn pixel tối đa mỗi chiều vùng quét spinner.</summary>
    public int PreviewSpinnerRegionMaxPx { get; set; } = 80;
    /// <summary>Luminance (0–255) coi là pixel tối (spinner đen).</summary>
    public int PreviewSpinnerDarkLuminanceMax { get; set; } = 80;
    /// <summary>Tỷ lệ % pixel tối tối thiểu trong vùng góc để coi là còn loading.</summary>
    public int PreviewSpinnerMinDarkPixelPercent { get; set; } = 3;
    /// <summary>Tỷ lệ % pixel tối tối đa — tránh nhầm vùng tối lớn (logo/banner đen).</summary>
    public int PreviewSpinnerMaxDarkPixelPercent { get; set; } = 40;
    /// <summary>Luminance trung bình vùng góc tối thiểu — nền phải chủ yếu sáng (trắng).</summary>
    public int PreviewSpinnerMinRegionAvgLuminance { get; set; } = 200;
    /// <summary>Nghỉ giữa các banner sau khi phân tích xong (giây). Florence local: 1–2 là đủ.</summary>
    public int GeminiDelaySeconds { get; set; } = 2;
    /// <summary>Bỏ qua creative nếu iframe preview nhỏ hơn ngưỡng (ví dụ 1x1).</summary>
    public int MinPreviewWidth { get; set; } = 100;
    public int MinPreviewHeight { get; set; } = 100;
    /// <summary>Sau khi hết nút "Display the next ad.", bấm Next page trên lưới listing và lặp.</summary>
    public bool EnableGridPagination { get; set; } = true;
    /// <summary>URL Ad review center mặc định khi message thiếu link_review.</summary>
    public string DefaultAdReviewCenterUrl { get; set; } =
        "https://admanager.google.com/27973503#creatives/ad_review_center/product=CONTENT";

    /// <summary>URL Ad review center (Unreviewed) cho worker Blocked khi không có link_review.</summary>
    public string BlockedReviewCenterUrl { get; set; } =
        "https://admanager.google.com/27973503#creatives/ad_review_center/product=CONTENT&as=Wg4YCyoKEAGYAgCyAgIBAg%253D%253D";
    /// <summary>Chờ sau Apply filter Creative ID trước khi Select all (giây).</summary>
    public int BlockedFilterApplyDelaySeconds { get; set; } = 2;
    /// <summary>TEST Blocked: số bản ghi Mongo is_review=0 tối đa mỗi lần chạy.</summary>
    public int BlockedTestMaxRecords { get; set; } = 50;
    /// <summary>Menu filter GAM — General ad category (Reviewed queue order).</summary>
    public string CategoryFilterMenuLabel { get; set; } = "General ad category";
    /// <summary>Chờ sau Apply filter category (giây).</summary>
    public int CategoryFilterApplyDelaySeconds { get; set; } = 2;
    /// <summary>TEST local (không có order trong args): category mặc định.</summary>
    public int DefaultReviewCategoryOrder { get; set; } = 1;
}
