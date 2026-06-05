using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Domain.Services;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class ReviewQueueMessageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ModeMismatch_ReturnsSkipped()
    {
        var processor = CreateProcessor(WorkerMode.Reviewed);

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
        var processor = CreateProcessor(WorkerMode.Reviewed);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://example.com",
            Mode = "unknown"
        });

        Assert.Equal(QueueProcessResult.InvalidMessage, result);
    }

    [Fact]
    public async Task ProcessAsync_ReviewedMode_UsesGamWorkflow()
    {
        var workflow = new FakeGamWorkflow();
        var processor = CreateProcessor(WorkerMode.Reviewed, workflow);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://admanager.google.com/27973503#creatives/ad_review_center",
            Mode = "reviewed"
        });

        Assert.Equal(QueueProcessResult.Processed, result);
        Assert.Equal(1, workflow.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_BlockedMode_UsesLinkFetcher()
    {
        var processor = CreateProcessor(WorkerMode.Blocked);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://example.com",
            Mode = "blocked"
        });

        Assert.Equal(QueueProcessResult.Processed, result);
    }

    private static ReviewQueueMessageProcessor CreateProcessor(
        WorkerMode workerMode,
        IGamAdReviewWorkflow? gamWorkflow = null)
    {
        var useCase = new ReviewBannerUseCase(
            new FakePolicyProvider(),
            new FakeImageReader(),
            new FakePromptBuilder(),
            new FakeVisionAnalyzer(),
            new VerdictParser(),
            new FakeTelegramNotifier());

        return new ReviewQueueMessageProcessor(
            gamWorkflow ?? new FakeGamWorkflow(),
            new FakeLinkImageFetcher("C:\\temp\\banner.png"),
            useCase,
            workerMode);
    }

    private sealed class FakeGamWorkflow : IGamAdReviewWorkflow
    {
        public int CallCount { get; private set; }

        public Task<GamReviewWorkflowResult> ProcessReviewListAsync(
            string listUrl,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new GamReviewWorkflowResult { ProcessedCount = 1 });
        }
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
