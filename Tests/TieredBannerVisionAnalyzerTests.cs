using Xunit;
using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Infrastructure.Florence;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Tests;

public class TieredBannerVisionAnalyzerTests
{
    private static readonly BannerImage TestImage = new(
        [0x89, 0x50, 0x4E, 0x47],
        "image/png",
        "test.png");

    [Fact]
    public async Task Allowed_SkipsGemini_ReturnsReviewed()
    {
        var holder = new TrackingModerationHolder();
        var analyzer = CreateAnalyzer(
            new FakeScanner(BannerModerationAction.Allowed),
            new FakeGeminiVerifier(shouldSucceed: true, "Blocked"),
            holder,
            geminiEnabled: true);

        var result = await analyzer.AnalyzeAsync(TestImage, "prompt");

        Assert.Equal("Reviewed", result);
        Assert.NotNull(holder.Last);
        Assert.Equal(ModerationFinalSource.FlorenceOnly, holder.Last!.FinalSource);
        Assert.False(holder.Last.GeminiAttempted);
    }

    [Fact]
    public async Task Blocked_GeminiSuccess_ReturnsGeminiVerdict()
    {
        var holder = new TrackingModerationHolder();
        var analyzer = CreateAnalyzer(
            new FakeScanner(BannerModerationAction.Blocked),
            new FakeGeminiVerifier(shouldSucceed: true, "Reviewed"),
            holder);

        var result = await analyzer.AnalyzeAsync(TestImage, "prompt");

        Assert.Equal("Reviewed", result);
        Assert.Equal(ModerationFinalSource.Gemini, holder.Last!.FinalSource);
        Assert.Equal("Reviewed", holder.Last.GeminiVerdict);
    }

    [Fact]
    public async Task Blocked_GeminiQuotaError_FallsBackToFlorenceBlocked()
    {
        var holder = new TrackingModerationHolder();
        var analyzer = CreateAnalyzer(
            new FakeScanner(BannerModerationAction.NeedsReview),
            new FakeGeminiVerifier(shouldSucceed: false, error: "429 rate limit exceeded"),
            holder);

        var result = await analyzer.AnalyzeAsync(TestImage, "prompt");

        Assert.Equal("Blocked", result);
        Assert.Equal(ModerationFinalSource.FlorenceFallback, holder.Last!.FinalSource);
        Assert.Contains("429", holder.Last.GeminiError);
    }

    [Fact]
    public async Task Blocked_GeminiDisabled_UsesFlorenceOnly()
    {
        var holder = new TrackingModerationHolder();
        var analyzer = CreateAnalyzer(
            new FakeScanner(BannerModerationAction.Blocked),
            new FakeGeminiVerifier(shouldSucceed: true, "Reviewed"),
            holder,
            geminiEnabled: false);

        var result = await analyzer.AnalyzeAsync(TestImage, "prompt");

        Assert.Equal("Blocked", result);
        Assert.Equal(ModerationFinalSource.FlorenceOnly, holder.Last!.FinalSource);
        Assert.False(holder.Last.GeminiAttempted);
    }

    private static TieredBannerVisionAnalyzer CreateAnalyzer(
        IBannerModerationScanner scanner,
        IGeminiBannerVerifier gemini,
        TrackingModerationHolder holder,
        bool geminiEnabled = true)
    {
        var bannerSettings = Options.Create(new BannerReviewSettings());
        var geminiSettings = Options.Create(new GeminiSettings { Enabled = geminiEnabled });

        return new TieredBannerVisionAnalyzer(
            scanner,
            holder,
            new FakePolicyProvider(),
            gemini,
            new TestOptionsMonitor<BannerReviewSettings>(bannerSettings),
            new TestOptionsMonitor<GeminiSettings>(geminiSettings),
            new NoOpTelegramNotifier());
    }

    private sealed class FakeScanner(BannerModerationAction action) : IBannerModerationScanner
    {
        public Task<BannerModerationResult> ScanAsync(
            BannerImage image,
            ReviewPolicy policy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BannerModerationResult
            {
                Action = action,
                Reason = $"Florence action={action}"
            });
    }

    private sealed class FakeGeminiVerifier(bool shouldSucceed, string? raw = null, string? error = null)
        : IGeminiBannerVerifier
    {
        public Task<GeminiVerifyAttempt> TryVerifyAsync(
            BannerImage image,
            string prompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeminiVerifyAttempt(shouldSucceed, raw, error));
    }

    private sealed class TrackingModerationHolder : IBannerModerationResultHolder
    {
        public BannerModerationResult? Last { get; private set; }

        public void SetLastResult(BannerModerationResult result) => Last = result;

        public BannerModerationResult? ConsumeLastResult()
        {
            var result = Last;
            Last = null;
            return result;
        }
    }

    private sealed class FakePolicyProvider : IReviewPolicyProvider
    {
        public Task<ReviewPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReviewPolicy("Blocked", "Reviewed", []));
    }

    private sealed class NoOpTelegramNotifier : ITelegramNotifier
    {
        public Task NotifyExceptionAsync(
            string step,
            Exception ex,
            ReviewTimingMetrics? timing = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyApiKeyIssueAsync(
            string message,
            ReviewTimingMetrics? timing = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyLlmNoResultAsync(
            string imagePath,
            string? rawText,
            ReviewTimingMetrics timing,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyReviewResultAsync(
            string imagePath,
            string verdictLabel,
            ReviewTimingMetrics timing,
            BannerModerationResult? moderation = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyBlockedActionAsync(string message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        private readonly IOptions<T> _options;

        public TestOptionsMonitor(IOptions<T> options) => _options = options;

        public T CurrentValue => _options.Value;

        public T Get(string? name) => _options.Value;

        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
