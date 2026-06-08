using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Lưu kết quả Florence scan gần nhất (một request review).</summary>
public interface IBannerModerationResultHolder
{
    void SetLastResult(BannerModerationResult result);

    /// <summary>Lấy và xóa kết quả — tránh dùng nhầm sang banner tiếp theo.</summary>
    BannerModerationResult? ConsumeLastResult();
}
