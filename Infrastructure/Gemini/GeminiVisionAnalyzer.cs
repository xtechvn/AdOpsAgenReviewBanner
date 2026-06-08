#if false
// Tạm thời tắt Gemini (có phí API). Dùng Florence-2 local — xem Infrastructure/Florence/.
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace AdOpsAgenReviewBanner.Infrastructure.Gemini;

/// <summary>
/// Triển khai IBannerVisionAnalyzer bằng Gemini generateContent + inline image.
/// Mỗi request random một API key; cache một Client cho mỗi key.
/// </summary>
public sealed class GeminiVisionAnalyzer : IBannerVisionAnalyzer, IDisposable
{
    private readonly IGeminiApiKeyProvider _apiKeyProvider;
    private readonly IOptionsMonitor<GeminiSettings> _geminiSettings;
    private readonly ITelegramNotifier _telegram;
    private readonly ConcurrentDictionary<string, Client> _clientsByKey = new();

    public GeminiVisionAnalyzer(
        IGeminiApiKeyProvider apiKeyProvider,
        IOptionsMonitor<GeminiSettings> geminiSettings,
        ITelegramNotifier telegram)
    {
        _apiKeyProvider = apiKeyProvider;
        _geminiSettings = geminiSettings;
        _telegram = telegram;
    }

    public async Task<string?> AnalyzeAsync(
        BannerImage image,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = GetClientForRequest();

            var contents = new List<Content>
            {
                new()
                {
                    Role = "user",
                    Parts =
                    [
                        new Part
                        {
                            InlineData = new Blob { Data = image.Bytes, MimeType = image.MimeType }
                        },
                        new Part { Text = prompt }
                    ]
                }
            };

            var config = new GenerateContentConfig
            {
                Temperature = 0,
                MaxOutputTokens = 32
            };

            GenerateContentResponse response = await client.Models.GenerateContentAsync(
                model: _geminiSettings.CurrentValue.Model,
                contents: contents,
                config: config);

            return response.Candidates?
                .FirstOrDefault()?.Content?.Parts?
                .FirstOrDefault(p => !string.IsNullOrEmpty(p.Text))?.Text;
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(AnalyzeAsync), ex, cancellationToken: cancellationToken);
            throw;
        }
    }

    private Client GetClientForRequest()
    {
        try
        {
            var apiKey = _apiKeyProvider.GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "Thiếu API key. Đặt Gemini:ApiKey trong appsettings (nhiều key cách nhau bởi dấu phẩy).");

            return _clientsByKey.GetOrAdd(apiKey, key => new Client(apiKey: key));
        }
        catch (Exception ex)
        {
            _ = _telegram.NotifyExceptionAsync(nameof(GetClientForRequest), ex);
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var client in _clientsByKey.Values)
            client.Dispose();

        _clientsByKey.Clear();
    }
}
#endif
