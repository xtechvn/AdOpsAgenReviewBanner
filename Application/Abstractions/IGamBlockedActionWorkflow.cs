using AdOpsAgenReviewBanner.Application.Queue;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Blocked worker: mở Ad review center → lọc Creative ID → Select all → Allow/Block trên GAM.
/// </summary>
public interface IGamBlockedActionWorkflow
{
    Task<GamBlockedActionResult> ApplyActionAsync(
        string creativeId,
        GamModerationAction action,
        string? linkReview = null,
        CancellationToken cancellationToken = default);
}

public sealed class GamBlockedActionResult
{
    public bool Success { get; init; }
    public string CreativeId { get; init; } = "";
    public string ActionLabel { get; init; } = "";
    public string? ErrorMessage { get; init; }
}
