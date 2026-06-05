using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Phân tích ảnh bằng model vision (DIP: Application không phụ thuộc Google.GenAI).
/// </summary>
public interface IBannerVisionAnalyzer
{
    /// <returns>Text thô từ model (mong đợi "Blocked" hoặc "Reviewed").</returns>
    Task<string?> AnalyzeAsync(
        BannerImage image,
        string prompt,
        CancellationToken cancellationToken = default);
}
