namespace AdOpsAgenReviewBanner.Application.Helpers;

/// <summary>Nhận diện lỗi Gemini nên fallback về Florence tier 1.</summary>
public static class GeminiFallbackDetector
{
    public static bool ShouldFallback(Exception ex)
    {
        if (ex is TaskCanceledException or OperationCanceledException)
            return true;

        return ApiKeyErrorDetector.IsApiKeyOrQuotaIssue(ex.Message);
    }

    public static bool ShouldFallback(string? errorMessage) =>
        ApiKeyErrorDetector.IsApiKeyOrQuotaIssue(errorMessage);
}
