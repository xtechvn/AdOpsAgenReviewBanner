using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Lấy keyword từ API (EN + VI). Khi lỗi trả null — dùng builtin.</summary>
public sealed class HttpKeywordCatalogProvider
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<KeywordApiSettings> _settings;

    public HttpKeywordCatalogProvider(HttpClient httpClient, IOptionsMonitor<KeywordApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public KeywordApiSettings Settings => _settings.CurrentValue;

    public async Task<KeywordCatalog?> FetchAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.CurrentValue;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.BaseUrl))
            return null;

        try
        {
            var url = CombineUrl(settings.BaseUrl, settings.KeywordsEndpoint);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds)));

            var dto = await _httpClient.GetFromJsonAsync<KeywordApiDto>(url, cts.Token);
            if (dto is null)
                return null;

            return new KeywordCatalog
            {
                BlockPhrases = dto.BlockPhrases ?? [],
                BlockStems = dto.BlockStems ?? [],
                BlockWords = dto.BlockWords ?? [],
                ReviewPhrases = dto.ReviewPhrases ?? [],
                ReviewStems = dto.ReviewStems ?? [],
                ReviewWords = dto.ReviewWords ?? []
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[KeywordApi] Không tải được keyword: {ex.Message}");
            return null;
        }
    }

    private static string CombineUrl(string baseUrl, string endpoint)
    {
        var root = baseUrl.TrimEnd('/');
        var path = endpoint.StartsWith('/') ? endpoint : "/" + endpoint;
        return root + path;
    }

    private sealed class KeywordApiDto
    {
        [JsonPropertyName("block_phrases")]
        public List<string>? BlockPhrases { get; set; }

        [JsonPropertyName("block_stems")]
        public List<string>? BlockStems { get; set; }

        [JsonPropertyName("block_words")]
        public List<string>? BlockWords { get; set; }

        [JsonPropertyName("review_phrases")]
        public List<string>? ReviewPhrases { get; set; }

        [JsonPropertyName("review_stems")]
        public List<string>? ReviewStems { get; set; }

        [JsonPropertyName("review_words")]
        public List<string>? ReviewWords { get; set; }
    }
}
