namespace AdOpsAgenReviewBanner.Domain.Models;

/// <summary>Danh mục vi phạm dùng trong prompt và so khớp policy (không phụ thuộc appsettings).</summary>
public sealed record BlockedCategory(
    string Name,
    string Description,
    IReadOnlyList<string> Keywords);
