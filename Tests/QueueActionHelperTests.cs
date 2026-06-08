using AdOpsAgenReviewBanner.Application.Queue;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class QueueActionHelperTests
{
    [Theory]
    [InlineData("Blocked", GamModerationAction.Block)]
    [InlineData("blocked", GamModerationAction.Block)]
    [InlineData("Reviewed", GamModerationAction.Allow)]
    [InlineData("Allow", GamModerationAction.Allow)]
    public void TryParse_ValidActions_ReturnsTrue(string input, GamModerationAction expected)
    {
        var ok = QueueActionHelper.TryParse(input, out var action);
        Assert.True(ok);
        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public void TryParse_Invalid_ReturnsFalse(string input)
    {
        Assert.False(QueueActionHelper.TryParse(input, out _));
    }
}
