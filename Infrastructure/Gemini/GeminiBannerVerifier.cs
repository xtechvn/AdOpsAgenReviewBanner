using System.Collections.Concurrent;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Gemini;

/// <summary>Gọi Gemini generateContent tier 2; trả Attempt thay vì ném khi lỗi thường gặp.</summary>
public sealed class GeminiBannerVerifier : IGeminiBannerVerifier, IDisposable
{
    private readonly IGeminiApiKeyProvider _apiKeyProvider;
    private readonly IOptionsMonitor<GeminiSettings> _geminiSettings;
    private readonly ConcurrentDictionary<string, Client> _clientsByKey = new();

    public GeminiBannerVerifier(
        IGeminiApiKeyProvider apiKeyProvider,
        IOptionsMonitor<GeminiSettings> geminiSettings)
    {
        _apiKeyProvider = apiKeyProvider;
        _geminiSettings = geminiSettings;
    }

    public async Task<GeminiVerifyAttempt> TryVerifyAsync(
        BannerImage image,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _apiKeyProvider.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new GeminiVerifyAttempt(false, null, "Thiếu Gemini API key.");

        var timeoutSeconds = Math.Max(5, _geminiSettings.CurrentValue.RequestTimeoutSeconds);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var client = _clientsByKey.GetOrAdd(apiKey, key => new Client(apiKey: key));
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

            var generateTask = client.Models.GenerateContentAsync(
                model: _geminiSettings.CurrentValue.Model,
                contents: contents,
                config: config);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
            var completed = await Task.WhenAny(generateTask, timeoutTask);
            if (completed != generateTask)
                return new GeminiVerifyAttempt(false, null, $"Gemini timeout sau {timeoutSeconds}s.");

            var response = await generateTask;
            var rawText = response.Candidates?
                .FirstOrDefault()?.Content?.Parts?
                .FirstOrDefault(p => !string.IsNullOrEmpty(p.Text))?.Text;

            if (string.IsNullOrWhiteSpace(rawText))
                return new GeminiVerifyAttempt(false, rawText, "Gemini không trả về text.");

            return new GeminiVerifyAttempt(true, rawText.Trim(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new GeminiVerifyAttempt(false, null, ex.Message);
        }
    }

    public void Dispose()
    {
        foreach (var client in _clientsByKey.Values)
            client.Dispose();

        _clientsByKey.Clear();
    }
}
