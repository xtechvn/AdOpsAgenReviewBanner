namespace AdOpsAgenReviewBanner.Configuration;

/// <summary>Cấu hình model Gemini (section "Gemini" trong appsettings.json).</summary>
public sealed class GeminiSettings
{
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Một hoặc nhiều API key Gemini, phân tách bằng dấu phẩy. Mỗi request chọn random một key.
    /// </summary>
    public string ApiKey { get; set; } = "";
}

/// <summary>Một danh mục nội dung bị chặn — map sang Domain.BlockedCategory.</summary>
public sealed class BlockedCategorySettings
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Keywords { get; set; } = [];
}

/// <summary>
/// Cấu hình review banner (section "BannerReview").
/// Đây là DTO cho JSON; logic nghiệp vụ dùng Domain.Models.ReviewPolicy.
/// </summary>
public sealed class BannerReviewSettings
{
    public string BlockedLabel { get; set; } = "Blocked";
    public string ReviewedLabel { get; set; } = "Reviewed";
    public List<BlockedCategorySettings> BlockedCategories { get; set; } = [];
}
