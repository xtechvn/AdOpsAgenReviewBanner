namespace AdOpsAgenReviewBanner.Application.Helpers;

/// <summary>Nhận diện lỗi API key / quota Gemini từ message exception.</summary>
internal static class ApiKeyErrorDetector
{
    private static readonly string[] KeyIssueMarkers =
    [
        "api key",
        "api_key",
        "quota",
        "expired",
        "invalid key",
        "permission_denied",
        "unauthorized",
        "401",
        "403",
        "billing",
        "rate limit",
        "429"
    ];

    public static bool IsApiKeyOrQuotaIssue(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return KeyIssueMarkers.Any(marker =>
            message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
