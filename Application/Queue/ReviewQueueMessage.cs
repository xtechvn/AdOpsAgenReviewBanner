using System.Text.Json.Serialization;

namespace AdOpsAgenReviewBanner.Application.Queue;

public sealed class ReviewQueueMessage
{
    [JsonPropertyName("link_review")]
    public string LinkReview { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";
}
