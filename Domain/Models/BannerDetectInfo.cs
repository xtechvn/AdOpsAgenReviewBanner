using System.Text.Json.Serialization;

namespace AdOpsAgenReviewBanner.Domain.Models;

/// <summary>
/// Nội dung JSON trong field <c>detect_info</c> của document Mongo banner review.
/// </summary>
public sealed class BannerDetectInfo
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("text_vietnamese")]
    public string TextVietnamese { get; set; } = "";

    [JsonPropertyName("time_detect_image")]
    public double TimeDetectImage { get; set; }

    [JsonPropertyName("time_detect_text")]
    public double TimeDetectText { get; set; }

    [JsonPropertyName("time_reg_eng")]
    public double TimeRegEng { get; set; }

    [JsonPropertyName("time_reg_vn")]
    public double TimeRegVn { get; set; }
}
