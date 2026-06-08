using System.Text.Json;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class ReviewQueueMessageTests
{
    [Fact]
    public void Deserialize_ReviewedJson_MapsLinkReviewModeAndOrder()
    {
        const string json = """
            {
              "link_review": "https://admanager.google.com/example",
              "mode": "reviewed",
              "order": 5
            }
            """;

        var message = JsonSerializer.Deserialize<ReviewQueueMessage>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(message);
        Assert.Equal("https://admanager.google.com/example", message!.LinkReview);
        Assert.Equal("reviewed", message.Mode);
        Assert.Equal(5, message.Order);
        Assert.True(message.IsValidForWorker(WorkerMode.Reviewed));
    }

    [Fact]
    public void ReviewedMessage_WithoutOrder_IsInvalid()
    {
        var message = new ReviewQueueMessage
        {
            LinkReview = "https://admanager.google.com/example",
            Mode = "reviewed"
        };

        Assert.False(message.IsValidForWorker(WorkerMode.Reviewed));
    }

    [Fact]
    public void ReviewedMessage_WithZeroOrder_IsInvalid()
    {
        var message = new ReviewQueueMessage
        {
            LinkReview = "https://admanager.google.com/example",
            Mode = "reviewed",
            Order = 0
        };

        Assert.False(message.IsValidForWorker(WorkerMode.Reviewed));
    }

    [Fact]
    public void Deserialize_ReviewedJson_MapsCategoryMetadata()
    {
        const string json = """
            {
              "link_review": "https://admanager.google.com/example",
              "mode": "reviewed",
              "order": 1,
              "_category": "Apparel",
              "_agen": "Ads ops 146"
            }
            """;

        var message = JsonSerializer.Deserialize<ReviewQueueMessage>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(message);
        Assert.Equal("Apparel", message!.Category);
    }

    [Fact]
    public void Deserialize_ExecutePlanJson_MapsCreativeIdActionAndMode()
    {
        const string json = """
            {
              "creative_id": "AJILAYpjouwEOlCWOS8HSQtTqEbOy4K5hw82N6pXjopruZWkouzCSlWv",
              "action": "Blocked",
              "mode": "execute_plan"
            }
            """;

        var message = JsonSerializer.Deserialize<ReviewQueueMessage>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(message);
        Assert.Equal("AJILAYpjouwEOlCWOS8HSQtTqEbOy4K5hw82N6pXjopruZWkouzCSlWv", message!.CreativeId);
        Assert.Equal("Blocked", message.Action);
        Assert.Equal("execute_plan", message.Mode);
        Assert.True(message.IsValidForWorker(WorkerMode.ExecutePlan));
    }
}
