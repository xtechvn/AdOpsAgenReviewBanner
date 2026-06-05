using AdOpsAgenReviewBanner.Application;
using Xunit;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Domain.Services;

namespace AdOpsAgenReviewBanner.Tests;

public class ReviewQueueMessageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ModeMismatch_ReturnsSkipped()
    {
        var processor = CreateProcessor(WorkerMode.Reviewed, new FakeLinkImageFetcher("C:\\temp\\banner.png"));

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://example.com",
            Mode = "blocked"
        });

        Assert.Equal(QueueProcessResult.SkippedModeMismatch, result);
    }

    [Fact]
    public async Task ProcessAsync_InvalidMode_ReturnsInvalidMessage()
    {
        var processor = CreateProcessor(WorkerMode.Reviewed, new FakeLinkImageFetcher("C:\\temp\\banner.png"));

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://example.com",
            Mode = "unknown"
        });

        Assert.Equal(QueueProcessResult.InvalidMessage, result);
    }

    [Fact]
    public async Task ProcessAsync_MatchingMode_ProcessesMessage()
    {
        var processor = CreateProcessor(WorkerMode.Blocked, new FakeLinkImageFetcher("C:\\temp\\banner.png"));

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://example.com",
            Mode = "blocked"
        });

        Assert.Equal(QueueProcessResult.Processed, result);
    }

    private static ReviewQueueMessageProcessor CreateProcessor(
        WorkerMode workerMode,
        ILinkImageFetcher linkImageFetcher)
    {
        var useCase = new ReviewBannerUseCase(
            new FakePolicyProvider(),
            new FakeImageReader(),
            new FakePromptBuilder(),
            new FakeVisionAnalyzer(),
            new VerdictParser(),
            new FakeTelegramNotifier());

        return new ReviewQueueMessageProcessor(linkImageFetcher, useCase, workerMode);
    }

    private sealed class FakeLinkImageFetcher(string path) : ILinkImageFetcher
    {
        public Task<string?> FetchToLocalPathAsync(string linkReview, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(path);
    }

    private sealed class FakeImageReader : IImageReader
    {
        public Task<BannerImage?> TryReadAsync(string imagePath, CancellationToken cancellationToken = default)
            => Task.FromResult<BannerImage?>(new BannerImage([1, 2, 3], "image/png", imagePath));
    }

    private sealed class FakePolicyProvider : IReviewPolicyProvider
    {
        public Task<ReviewPolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReviewPolicy("Blocked", "Reviewed", []));
    }

    private sealed class FakePromptBuilder : IPromptBuilder
    {
        public string Build(ReviewPolicy policy) => "prompt";
    }

    private sealed class FakeVisionAnalyzer : IBannerVisionAnalyzer
    {
        public Task<string?> AnalyzeAsync(BannerImage image, string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("Reviewed");
    }

    private sealed class FakeTelegramNotifier : ITelegramNotifier
    {
        public Task NotifyApiKeyIssueAsync(string details, ReviewTimingMetrics? timing = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyExceptionAsync(string context, Exception exception, ReviewTimingMetrics? timing = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyLlmNoResultAsync(string imagePath, string? rawResponse, ReviewTimingMetrics timing, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyReviewResultAsync(string imagePath, string verdictLabel, ReviewTimingMetrics timing, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
