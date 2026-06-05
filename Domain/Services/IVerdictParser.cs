using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Domain.Services;

/// <summary>Chuẩn hóa text thô từ model thành Blocked hoặc Reviewed.</summary>
public interface IVerdictParser
{
    BannerVerdictKind? Parse(string? rawResponse, ReviewPolicy policy);
}
