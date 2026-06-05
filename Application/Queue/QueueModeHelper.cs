using AdOpsAgenReviewBanner.Configuration;

namespace AdOpsAgenReviewBanner.Application.Queue;

public static class QueueModeHelper
{
    public static bool TryParse(string? value, out WorkerMode mode)
    {
        mode = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Equals("reviewed", StringComparison.OrdinalIgnoreCase))
        {
            mode = WorkerMode.Reviewed;
            return true;
        }

        if (value.Equals("blocked", StringComparison.OrdinalIgnoreCase))
        {
            mode = WorkerMode.Blocked;
            return true;
        }

        return false;
    }
}
