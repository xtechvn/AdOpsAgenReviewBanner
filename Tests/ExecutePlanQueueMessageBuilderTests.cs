using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class ExecutePlanQueueMessageBuilderTests
{
    [Theory]
    [InlineData(true, "Blocked")]
    [InlineData(false, "Reviewed")]
    public void FromMongoReview_MapsActionFromIsBlockAds(bool isBlockAds, string expectedAction)
    {
        var message = ExecutePlanQueueMessageBuilder.FromMongoReview(new ExecutePlanPublishRequest
        {
            CreativeId = "CREATIVE_123",
            IsBlockAds = isBlockAds,
            LinkReview = "https://admanager.google.com/27973503#creatives/ad_review_center",
            Order = 2,
            Category = "Apparel"
        });

        Assert.Equal("CREATIVE_123", message.CreativeId);
        Assert.Equal(expectedAction, message.Action);
        Assert.Equal("execute_plan", message.Mode);
        Assert.Equal("https://admanager.google.com/27973503#creatives/ad_review_center", message.LinkReview);
        Assert.Equal(2, message.Order);
        Assert.Equal("Apparel", message.Category);
        Assert.True(message.IsValidForWorker(WorkerMode.ExecutePlan));
    }

    [Fact]
    public void FromMongoReview_WhenLinkReviewNull_UsesDefaultUrl()
    {
        var message = ExecutePlanQueueMessageBuilder.FromMongoReview(new ExecutePlanPublishRequest
        {
            CreativeId = "CREATIVE_123",
            IsBlockAds = false,
            LinkReview = null
        });

        Assert.Equal(GamReviewLinkResolver.DefaultAdReviewCenterUrl, message.LinkReview);
    }
}
