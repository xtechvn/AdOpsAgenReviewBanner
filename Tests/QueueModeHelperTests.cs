using AdOpsAgenReviewBanner.Application.Queue;
using Xunit;
using AdOpsAgenReviewBanner.Configuration;

namespace AdOpsAgenReviewBanner.Tests;

public class QueueModeHelperTests
{
    [Theory]
    [InlineData("reviewed", WorkerMode.Reviewed)]
    [InlineData("Reviewed", WorkerMode.Reviewed)]
    [InlineData("blocked", WorkerMode.Blocked)]
    [InlineData("BLOCKED", WorkerMode.Blocked)]
    public void TryParse_ValidModes_ReturnsTrue(string input, WorkerMode expected)
    {
        var ok = QueueModeHelper.TryParse(input, out var mode);
        Assert.True(ok);
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("pending")]
    public void TryParse_InvalidModes_ReturnsFalse(string input)
    {
        var ok = QueueModeHelper.TryParse(input, out _);
        Assert.False(ok);
    }
}
