using AdOpsAgenReviewBanner.Configuration;

namespace AdOpsAgenReviewBanner.Application.Queue;

public static class QueueModeHelper
{
    public const string ExecutePlanModeValue = "execute_plan";
    public const string ReviewedModeValue = "reviewed";

    public static bool TryParse(string? value, out WorkerMode mode)
    {
        mode = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Equals(ReviewedModeValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = WorkerMode.Reviewed;
            return true;
        }

        if (value.Equals(ExecutePlanModeValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = WorkerMode.ExecutePlan;
            return true;
        }

        return false;
    }
}
