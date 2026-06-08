namespace AdOpsAgenReviewBanner.Application.Queue;

/// <summary>Allow/Block từ queue — Allow tương đương Reviewed trên GAM.</summary>
public enum GamModerationAction
{
    Allow,
    Block
}

public static class QueueActionHelper
{
    public static bool TryParse(string? value, out GamModerationAction action)
    {
        action = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Equals("blocked", StringComparison.OrdinalIgnoreCase)
            || value.Equals("block", StringComparison.OrdinalIgnoreCase))
        {
            action = GamModerationAction.Block;
            return true;
        }

        if (value.Equals("reviewed", StringComparison.OrdinalIgnoreCase)
            || value.Equals("allow", StringComparison.OrdinalIgnoreCase))
        {
            action = GamModerationAction.Allow;
            return true;
        }

        return false;
    }
}
