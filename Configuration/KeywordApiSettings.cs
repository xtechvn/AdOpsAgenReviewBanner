namespace AdOpsAgenReviewBanner.Configuration;

/// <summary>API keyword block/review (EN + VI) — bật khi có endpoint.</summary>
public sealed class KeywordApiSettings
{
    public bool Enabled { get; set; }

    /// <summary>Base URL, ví dụ https://api.example.com</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>GET trả JSON keyword — mặc định /api/banner-keywords</summary>
    public string KeywordsEndpoint { get; set; } = "/api/banner-keywords";

    public int CacheMinutes { get; set; } = 30;

    public int TimeoutSeconds { get; set; } = 15;
}
