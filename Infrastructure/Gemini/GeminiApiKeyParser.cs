namespace AdOpsAgenReviewBanner.Infrastructure.Gemini;

/// <summary>Parse danh sách API key phân tách bằng dấu phẩy.</summary>
internal static class GeminiApiKeyParser
{
    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
