using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

public interface IBannerReviewRepository
{
    Task<bool> ExistsByCreativeIdAsync(string creativeId, CancellationToken cancellationToken = default);

    /// <summary>Trả về tập iframe_id đã tồn tại trong Mongo (batch lookup listing grid).</summary>
    Task<HashSet<string>> FindExistingIframeIdsAsync(
        IEnumerable<string> iframeIds,
        CancellationToken cancellationToken = default);

    /// <returns>true nếu đã insert; false nếu Mongo tắt hoặc bỏ qua.</returns>
    Task<bool> InsertAsync(BannerReviewDocument document, CancellationToken cancellationToken = default);

    /// <summary>Lấy banner chưa apply lên GAM (is_review = 0).</summary>
    Task<IReadOnlyList<BannerReviewDocument>> FindPendingGamReviewAsync(
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu đã review trên GAM theo creative_id.</summary>
    Task<bool> MarkReviewedByCreativeIdAsync(
        string creativeId,
        CancellationToken cancellationToken = default);
}
