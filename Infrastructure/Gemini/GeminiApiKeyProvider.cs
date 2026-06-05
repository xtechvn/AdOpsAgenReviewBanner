using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Gemini;

/// <summary>Lấy một API key Gemini (random từ pool nếu có nhiều key).</summary>
public interface IGeminiApiKeyProvider
{
    /// <summary>Chọn ngẫu nhiên một key từ danh sách cấu hình.</summary>
    string? GetApiKey();
}

/// <summary>
/// Đọc danh sách key từ appsettings → Gemini:ApiKey (phân tách bằng dấu phẩy).
/// Mỗi lần gọi GetApiKey() chọn random một key.
/// GEMINI_API_KEY / GOOGLE_API_KEY (cũng hỗ trợ nhiều key cách nhau bởi dấu phẩy) ghi đè appsettings.
/// </summary>
public sealed class RandomGeminiApiKeyProvider : IGeminiApiKeyProvider
{
    private readonly IOptionsMonitor<GeminiSettings> _settings;

    public RandomGeminiApiKeyProvider(IOptionsMonitor<GeminiSettings> settings)
    {
        _settings = settings;
    }

    public string? GetApiKey()
    {
        var keys = ResolveAllKeys();
        if (keys.Count == 0)
            return null;

        var index = Random.Shared.Next(keys.Count);
        return keys[index];
    }

    private IReadOnlyList<string> ResolveAllKeys()
    {
        var envRaw = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        if (!string.IsNullOrWhiteSpace(envRaw))
            return GeminiApiKeyParser.Parse(envRaw);

        return GeminiApiKeyParser.Parse(_settings.CurrentValue.ApiKey);
    }
}
