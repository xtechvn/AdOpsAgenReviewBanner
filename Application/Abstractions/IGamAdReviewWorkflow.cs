namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Mở link_review (danh sách GAM), duyệt từng banner qua lightbox + nút Next, review và lưu Mongo.
/// </summary>
public interface IGamAdReviewWorkflow
{
    Task<GamReviewWorkflowResult> ProcessReviewListAsync(
        string listUrl,
        CancellationToken cancellationToken = default);
}

public sealed class GamReviewWorkflowResult
{
    public int ProcessedCount { get; init; }
    public int SkippedExistingCount { get; init; }
    public int ReviewedCount { get; init; }
    public int ErrorCount { get; init; }
}
