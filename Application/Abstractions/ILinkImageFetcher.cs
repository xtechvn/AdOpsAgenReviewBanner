namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Chuyển link review thành đường dẫn ảnh local để dùng lại pipeline hiện tại.
/// </summary>
public interface ILinkImageFetcher
{
    Task<string?> FetchToLocalPathAsync(
        string linkReview,
        CancellationToken cancellationToken = default);
}
