using System.Text.Json.Serialization;
using AdOpsAgenReviewBanner.Configuration;

namespace AdOpsAgenReviewBanner.Application.Queue;

public sealed class ReviewQueueMessage
{
    [JsonPropertyName("link_review")]
    public string LinkReview { get; set; } = "";

    /// <summary>Thứ tự category trong General ad category (1-based). Bắt buộc với mode reviewed.</summary>
    [JsonPropertyName("order")]
    public int? Order { get; set; }

    /// <summary>Tên category GAM từ publisher (n8n). Lưu vào Mongo <c>category_name</c>.</summary>
    [JsonPropertyName("_category")]
    public string? Category { get; set; }

    [JsonPropertyName("creative_id")]
    public string CreativeId { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    public bool IsValidForWorker(WorkerMode workerMode)
    {
        if (!QueueModeHelper.TryParse(Mode, out _))
            return false;

        return workerMode switch
        {
            WorkerMode.Reviewed => !string.IsNullOrWhiteSpace(LinkReview)
                && Order is > 0,
            WorkerMode.ExecutePlan => !string.IsNullOrWhiteSpace(CreativeId)
                && QueueActionHelper.TryParse(Action, out _),
            _ => false
        };
    }
}
