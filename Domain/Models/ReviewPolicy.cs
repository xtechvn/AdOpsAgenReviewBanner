namespace AdOpsAgenReviewBanner.Domain.Models;

/// <summary>
/// Chính sách review: nhãn kết quả và danh sách danh mục bị block.
/// Nguồn dữ liệu có thể là appsettings hoặc API (qua IReviewPolicyProvider).
/// </summary>
public sealed record ReviewPolicy(
    string BlockedLabel,
    string ReviewedLabel,
    IReadOnlyList<BlockedCategory> Categories);
