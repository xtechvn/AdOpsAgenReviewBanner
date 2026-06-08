using AdOpsAgenReviewBanner.Configuration;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class RabbitMqSettingsTests
{
    [Fact]
    public void ResolveConsumerQueueName_Reviewed_UsesQueueName()
    {
        var settings = new RabbitMqSettings
        {
            QueueName = "PROCESS_REVIEW_BANNER_DFP",
            ExecutePlanQueueName = "PROCESS_EXECUTE_PLAN_DFP"
        };

        Assert.Equal("PROCESS_REVIEW_BANNER_DFP", settings.ResolveConsumerQueueName(WorkerMode.Reviewed));
    }

    [Fact]
    public void ResolveConsumerQueueName_ExecutePlan_UsesExecutePlanQueueName()
    {
        var settings = new RabbitMqSettings
        {
            QueueName = "PROCESS_REVIEW_BANNER_DFP",
            ExecutePlanQueueName = "PROCESS_EXECUTE_PLAN_DFP"
        };

        Assert.Equal("PROCESS_EXECUTE_PLAN_DFP", settings.ResolveConsumerQueueName(WorkerMode.ExecutePlan));
    }
}
