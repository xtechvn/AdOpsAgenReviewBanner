using Xunit;
using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Domain.Services;

namespace AdOpsAgenReviewBanner.Tests;

public class ReviewBannerUseCaseTelegramTests
{
    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsTelegramWithImagePath()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"usecase-test-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        try
        {
            var telegram = new TrackingTelegramNotifier();
            var useCase = new ReviewBannerUseCase(
                new FakePolicyProvider(),
                new FakeImageReader(imagePath),
                new FakePromptBuilder(),
                new FakeVisionAnalyzer("Reviewed"),
                new VerdictParser(),
                new FakeModerationResultHolder(),
                telegram);

            var outcome = await useCase.ExecuteAsync(imagePath);

            var success = Assert.IsType<ReviewBannerOutcome.Success>(outcome);
            Assert.Equal(BannerVerdictKind.Reviewed, success.Verdict);
            Assert.Equal(1, telegram.ReviewResultCallCount);
            Assert.Equal(imagePath, telegram.LastImagePath);
            Assert.Equal("Reviewed", telegram.LastVerdictLabel);
        }
        finally
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenBlocked_CallsTelegramWithBlockedLabel()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"usecase-blocked-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            var telegram = new TrackingTelegramNotifier();
            var useCase = new ReviewBannerUseCase(
                new FakePolicyProvider(),
                new FakeImageReader(imagePath),
                new FakePromptBuilder(),
                new FakeVisionAnalyzer("Blocked"),
                new VerdictParser(),
                new FakeModerationResultHolder(),
                telegram);

            var outcome = await useCase.ExecuteAsync(imagePath);

            var success = Assert.IsType<ReviewBannerOutcome.Success>(outcome);
            Assert.Equal(BannerVerdictKind.Blocked, success.Verdict);
            Assert.Equal("Blocked", telegram.LastVerdictLabel);
            Assert.Equal(imagePath, telegram.LastImagePath);
        }
        finally
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }
    }

    private sealed class TrackingTelegramNotifier : ITelegramNotifier
    {
        public int ReviewResultCallCount { get; private set; }
        public string? LastImagePath { get; private set; }
        public string? LastVerdictLabel { get; private set; }

        public Task NotifyApiKeyIssueAsync(string details, ReviewTimingMetrics? timing = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyExceptionAsync(string context, Exception exception, ReviewTimingMetrics? timing = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyLlmNoResultAsync(string imagePath, string? rawResponse, ReviewTimingMetrics timing, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyReviewResultAsync(
            string imagePath,
            string verdictLabel,
            ReviewTimingMetrics timing,
            BannerModerationResult? moderation = null,
            CancellationToken cancellationToken = default)
        {
            ReviewResultCallCount++;
            LastImagePath = imagePath;
            LastVerdictLabel = verdictLabel;
            return Task.CompletedTask;
        }

        public Task NotifyBlockedActionAsync(string message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeModerationResultHolder : IBannerModerationResultHolder
    {
        public void SetLastResult(BannerModerationResult result) { }

        public BannerModerationResult? ConsumeLastResult() => null;
    }

    private sealed class FakePolicyProvider : IReviewPolicyProvider
    {
        public Task<ReviewPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReviewPolicy("Blocked", "Reviewed", []));
    }

    private sealed class FakeImageReader(string path) : IImageReader
    {
        public Task<BannerImage?> TryReadAsync(string imagePath, CancellationToken cancellationToken = default)
            => Task.FromResult<BannerImage?>(new BannerImage([1, 2, 3], "image/png", path));
    }

    private sealed class FakePromptBuilder : IPromptBuilder
    {
        public string Build(ReviewPolicy policy) => "prompt";
    }

    private sealed class FakeVisionAnalyzer(string label) : IBannerVisionAnalyzer
    {
        public Task<string?> AnalyzeAsync(BannerImage image, string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(label);
    }
}
