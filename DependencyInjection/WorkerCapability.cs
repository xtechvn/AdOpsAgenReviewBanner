using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Configuration;

namespace AdOpsAgenReviewBanner.DependencyInjection;

/// <summary>Phân tách capability theo WorkerMode — ExecutePlan không cần Florence.</summary>
public static class WorkerCapability
{
    public static WorkerMode ResolveWorkerMode(IConfiguration configuration) =>
        configuration.GetSection("Runtime").GetValue<WorkerMode?>("WorkerMode")
        ?? WorkerMode.Reviewed;

    public static bool RequiresFlorence(WorkerMode workerMode) =>
        workerMode != WorkerMode.ExecutePlan;
}
