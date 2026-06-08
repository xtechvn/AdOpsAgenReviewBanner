namespace AdOpsAgenReviewBanner.Application.Queue;

public static class ExecutePlanQueueMessageBuilder
{
    public static ReviewQueueMessage FromMongoReview(ExecutePlanPublishRequest request) =>
        new()
        {
            LinkReview = GamReviewLinkResolver.Resolve(request.LinkReview),
            Order = request.Order,
            Category = request.Category,
            CreativeId = request.CreativeId,
            Action = request.IsBlockAds ? "Blocked" : "Reviewed",
            Mode = QueueModeHelper.ExecutePlanModeValue
        };
}
