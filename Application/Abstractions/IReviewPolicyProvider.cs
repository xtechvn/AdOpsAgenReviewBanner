using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>
/// Cung cấp chính sách review (Strategy).
/// Hiện tại: appsettings; sau này: REST API quản lý keywords.
/// </summary>
public interface IReviewPolicyProvider
{
    Task<ReviewPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
}
