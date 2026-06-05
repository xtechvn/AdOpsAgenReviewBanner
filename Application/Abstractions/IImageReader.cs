using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Đọc ảnh banner từ nguồn (hiện tại: file local).</summary>
public interface IImageReader
{
    /// <returns>null nếu file không tồn tại.</returns>
    Task<BannerImage?> TryReadAsync(string imagePath, CancellationToken cancellationToken = default);
}
