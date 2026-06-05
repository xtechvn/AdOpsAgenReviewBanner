using System.Text.Json;
using Xunit;
using AdOpsAgenReviewBanner.Application.Queue;

namespace AdOpsAgenReviewBanner.Tests;

public class ReviewQueueMessageTests
{
    [Fact]
    public void Deserialize_SnakeCaseJson_MapsLinkReviewAndMode()
    {
        const string json = """
            {
              "link_review": "https://admanager.google.com/example",
              "mode": "reviewed"
            }
            """;

        var message = JsonSerializer.Deserialize<ReviewQueueMessage>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(message);
        Assert.Equal("https://admanager.google.com/example", message!.LinkReview);
        Assert.Equal("reviewed", message.Mode);
    }
}
