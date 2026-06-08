namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Mở link_review (danh sách GAM), duyệt từng banner qua lightbox + nút Next, review và lưu Mongo.
/// </summary>
public interface IGamAdReviewWorkflow
{
    /// <param name="categoryOrder">Thứ tự General ad category trên GAM (1-based).</param>
    /// <param name="categoryName">Tên category từ message queue (<c>_category</c>), lưu Mongo <c>category_name</c>.</param>
    Task<GamReviewWorkflowResult> ProcessReviewListAsync(
        string listUrl,
        int categoryOrder,
        string? categoryName = null,
        CancellationToken cancellationToken = default);
}

public sealed class GamReviewWorkflowResult
{
    public int ProcessedCount { get; init; }
    public int SkippedExistingCount { get; init; }
    /// <summary>Preview 1x1, missing preview, hoặc không có iframe — không gọi Florence.</summary>
    public int SkippedPreviewCount { get; init; }
    public int ReviewedCount { get; init; }
    public int ErrorCount { get; init; }
    /// <summary>Số trang lưới GAM đã quét (mỗi lần Next page +1).</summary>
    public int GridPagesProcessed { get; init; }
}
