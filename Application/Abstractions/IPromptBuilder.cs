using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Tạo prompt gửi Gemini từ ReviewPolicy (có thể thay implementation cho prompt dài/ngắn).</summary>
public interface IPromptBuilder
{
    string Build(ReviewPolicy policy);
}
