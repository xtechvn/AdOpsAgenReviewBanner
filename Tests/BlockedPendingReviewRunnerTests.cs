using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class BlockedPendingReviewRunnerTests
{
    [Fact]
    public async Task RunAsync_PendingRecords_UsesIsBlockAdsForAction_AndMarksReviewed()
    {
        var repository = new TrackingRepository
        {
            Pending =
            [
                new BannerReviewDocument
                {
                    creative_id = "CID_BLOCK",
                    is_block_ads = true,
                    is_review = 0
                },
                new BannerReviewDocument
                {
                    creative_id = "CID_ALLOW",
                    is_block_ads = false,
                    is_review = 0
                }
            ]
        };
        var blocked = new TrackingBlockedWorkflow();
        var runner = new BlockedPendingReviewRunner(
            repository,
            blocked,
            Options.Create(new GamReviewSettings { BlockedTestMaxRecords = 10 }));

        var result = await runner.RunAsync();

        Assert.Equal(2, result.Found);
        Assert.Equal(2, result.Processed);
        Assert.Equal(2, result.MarkedReviewed);
        Assert.Equal(0, result.Errors);
        Assert.Equal(GamModerationAction.Block, blocked.Actions["CID_BLOCK"]);
        Assert.Equal(GamModerationAction.Allow, blocked.Actions["CID_ALLOW"]);
    }

    private sealed class TrackingRepository : IBannerReviewRepository
    {
        public IReadOnlyList<BannerReviewDocument> Pending { get; init; } = [];

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
            Task.FromResult(Pending);

        public Task<bool> MarkReviewedByCreativeIdAsync(
            string creativeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class TrackingBlockedWorkflow : IGamBlockedActionWorkflow
    {
        public Dictionary<string, GamModerationAction> Actions { get; } = new();

        public Task<GamBlockedActionResult> ApplyActionAsync(
            string creativeId,
            GamModerationAction action,
            string? linkReview = null,
            CancellationToken cancellationToken = default)
        {
            Actions[creativeId] = action;
            return Task.FromResult(new GamBlockedActionResult
            {
                Success = true,
                CreativeId = creativeId,
                ActionLabel = action == GamModerationAction.Block ? "Blocked" : "Reviewed"
            });
        }
    }
}
