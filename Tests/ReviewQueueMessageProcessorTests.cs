using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
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
            Mode = "execute_plan",
            CreativeId = "AJILAYtest",
            Action = "Blocked"
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
            Mode = "reviewed",
            Order = 3,
            Category = "Autos & Vehicles"
        });

        Assert.Equal(QueueProcessResult.Processed, result);
        Assert.Equal(1, workflow.CallCount);
        Assert.Equal(3, workflow.LastCategoryOrder);
        Assert.Equal("Autos & Vehicles", workflow.LastCategoryName);
    }

    [Fact]
    public async Task ProcessAsync_ReviewedMode_MissingLink_ReturnsInvalid()
    {
        var processor = CreateProcessor(WorkerMode.Reviewed);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            Mode = "reviewed",
            Order = 1
        });

        Assert.Equal(QueueProcessResult.InvalidMessage, result);
    }

    [Fact]
    public async Task ProcessAsync_ReviewedMode_MissingOrder_ReturnsInvalid()
    {
        var processor = CreateProcessor(WorkerMode.Reviewed);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            LinkReview = "https://example.com",
            Mode = "reviewed"
        });

        Assert.Equal(QueueProcessResult.InvalidMessage, result);
    }

    [Fact]
    public async Task ProcessAsync_ExecutePlanMode_UsesBlockedWorkflow_AndMarksReviewed()
    {
        var blocked = new FakeBlockedWorkflow { Success = true };
        var repository = new FakeBannerReviewRepository();
        var processor = CreateProcessor(WorkerMode.ExecutePlan, blockedWorkflow: blocked, repository: repository);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            CreativeId = "AJILAYpjouwEOlCWOS8HSQtTqEbOy4K5hw82N6pXjopruZWkouzCSlWv",
            Action = "Blocked",
            Mode = "execute_plan"
        });

        Assert.Equal(QueueProcessResult.Processed, result);
        Assert.Equal(1, blocked.CallCount);
        Assert.Equal(GamModerationAction.Block, blocked.LastAction);
        Assert.Contains("AJILAYpjouwEOlCWOS8HSQtTqEbOy4K5hw82N6pXjopruZWkouzCSlWv", repository.MarkedReviewedIds);
    }

    [Fact]
    public async Task ProcessAsync_ExecutePlanMode_AllowAction_MapsToAllow()
    {
        var blocked = new FakeBlockedWorkflow { Success = true };
        var processor = CreateProcessor(WorkerMode.ExecutePlan, blockedWorkflow: blocked);

        await processor.ProcessAsync(new ReviewQueueMessage
        {
            CreativeId = "AJILAYtest",
            Action = "Reviewed",
            Mode = "execute_plan"
        });

        Assert.Equal(GamModerationAction.Allow, blocked.LastAction);
    }

    [Fact]
    public async Task ProcessAsync_ExecutePlanMode_MissingCreativeId_ReturnsInvalid()
    {
        var processor = CreateProcessor(WorkerMode.ExecutePlan);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            Action = "Blocked",
            Mode = "execute_plan"
        });

        Assert.Equal(QueueProcessResult.InvalidMessage, result);
    }

    [Fact]
    public async Task ProcessAsync_ExecutePlanMode_WorkflowFails_ReturnsBlockedActionFailed()
    {
        var blocked = new FakeBlockedWorkflow { Success = false };
        var processor = CreateProcessor(WorkerMode.ExecutePlan, blockedWorkflow: blocked);

        var result = await processor.ProcessAsync(new ReviewQueueMessage
        {
            CreativeId = "AJILAYtest",
            Action = "Blocked",
            Mode = "execute_plan"
        });

        Assert.Equal(QueueProcessResult.BlockedActionFailed, result);
    }

    private static ReviewQueueMessageProcessor CreateProcessor(
        WorkerMode workerMode,
        IGamAdReviewWorkflow? gamWorkflow = null,
        IGamBlockedActionWorkflow? blockedWorkflow = null,
        IBannerReviewRepository? repository = null) =>
        new(
            workerMode == WorkerMode.Reviewed
                ? gamWorkflow ?? new FakeGamWorkflow()
                : null,
            workerMode == WorkerMode.ExecutePlan
                ? blockedWorkflow ?? new FakeBlockedWorkflow { Success = true }
                : null,
            repository ?? new FakeBannerReviewRepository(),
            workerMode);

    private sealed class FakeGamWorkflow : IGamAdReviewWorkflow
    {
        public int CallCount { get; private set; }
        public int LastCategoryOrder { get; private set; }
        public string? LastCategoryName { get; private set; }

        public Task<GamReviewWorkflowResult> ProcessReviewListAsync(
            string listUrl,
            int categoryOrder,
            string? categoryName = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCategoryOrder = categoryOrder;
            LastCategoryName = categoryName;
            return Task.FromResult(new GamReviewWorkflowResult { ProcessedCount = 1 });
        }
    }

    private sealed class FakeBlockedWorkflow : IGamBlockedActionWorkflow
    {
        public bool Success { get; init; } = true;
        public int CallCount { get; private set; }
        public GamModerationAction LastAction { get; private set; }

        public Task<GamBlockedActionResult> ApplyActionAsync(
            string creativeId,
            GamModerationAction action,
            string? linkReview = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAction = action;
            return Task.FromResult(new GamBlockedActionResult
            {
                Success = Success,
                CreativeId = creativeId,
                ActionLabel = action == GamModerationAction.Block ? "Blocked" : "Reviewed",
                ErrorMessage = Success ? null : "selenium error"
            });
        }
    }

    private sealed class FakeBannerReviewRepository : IBannerReviewRepository
    {
        public List<string> MarkedReviewedIds { get; } = [];

        public Task<bool> ExistsByCreativeIdAsync(string creativeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<HashSet<string>> FindExistingIframeIdsAsync(
            IEnumerable<string> iframeIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<HashSet<string>>([]);

        public Task<bool> InsertAsync(BannerReviewDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<BannerReviewDocument>> FindPendingGamReviewAsync(
            int maxCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BannerReviewDocument>>([]);

        public Task<bool> MarkReviewedByCreativeIdAsync(
            string creativeId,
            CancellationToken cancellationToken = default)
        {
            MarkedReviewedIds.Add(creativeId);
            return Task.FromResult(true);
        }
    }
}
