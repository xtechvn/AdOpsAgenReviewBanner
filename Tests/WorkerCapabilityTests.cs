using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class WorkerCapabilityTests
{
    [Theory]
    [InlineData("Reviewed", true)]
    [InlineData("ExecutePlan", false)]
    public void RequiresFlorence_MatchesWorkerMode(string workerMode, bool expected)
    {
        Assert.Equal(expected, WorkerCapability.RequiresFlorence(Enum.Parse<WorkerMode>(workerMode)));
    }

    [Fact]
    public void ResolveWorkerMode_ReadsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:WorkerMode"] = "ExecutePlan"
            })
            .Build();

        Assert.Equal(WorkerMode.ExecutePlan, WorkerCapability.ResolveWorkerMode(config));
    }
}
