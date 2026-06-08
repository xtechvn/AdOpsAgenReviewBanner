namespace AdOpsAgenReviewBanner.Application.Queue;

/// <summary>Thông tin publish message execute_plan sau khi Reviewed lưu Mongo.</summary>
public sealed class ExecutePlanPublishRequest
{
    public required string CreativeId { get; init; }
    public required bool IsBlockAds { get; init; }
    public string? LinkReview { get; init; }
    public int? Order { get; init; }
    public string? Category { get; init; }
}
