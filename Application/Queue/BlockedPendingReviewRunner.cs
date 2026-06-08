using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Application.Queue;

/// <summary>TEST Blocked: quét Mongo is_review=0 → apply Allow/Block trên GAM.</summary>
public sealed class BlockedPendingReviewRunner
{
    private readonly IBannerReviewRepository _repository;
    private readonly IGamBlockedActionWorkflow _blockedWorkflow;
    private readonly GamReviewSettings _gamSettings;

    public BlockedPendingReviewRunner(
        IBannerReviewRepository repository,
        IGamBlockedActionWorkflow blockedWorkflow,
        IOptions<GamReviewSettings> gamSettings)
    {
        _repository = repository;
        _blockedWorkflow = blockedWorkflow;
        _gamSettings = gamSettings.Value;
    }

    public async Task<BlockedPendingBatchResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.FindPendingGamReviewAsync(
            _gamSettings.BlockedTestMaxRecords,
            cancellationToken);

        var result = new BlockedPendingBatchResult { Found = pending.Count };
        if (pending.Count == 0)
        {
            Console.WriteLine("[Blocked TEST] Không có bản ghi is_review=0 trong Mongo.");
            return result;
        }

        Console.WriteLine($"[Blocked TEST] Tìm thấy {pending.Count} bản ghi is_review=0 — bắt đầu xử lý GAM.");

        foreach (var doc in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var action = doc.is_block_ads ? GamModerationAction.Block : GamModerationAction.Allow;
            var actionLabel = action == GamModerationAction.Block ? "Blocked" : "Reviewed";
            Console.WriteLine(
                $"[Blocked TEST] creative_id={doc.creative_id}, is_block_ads={doc.is_block_ads} → GAM {actionLabel}");

            var gamResult = await _blockedWorkflow.ApplyActionAsync(
                doc.creative_id,
                action,
                linkReview: null,
                cancellationToken);

            if (!gamResult.Success)
            {
                result.Errors++;
                Console.Error.WriteLine(
                    $"[Blocked TEST] Thất bại creative_id={doc.creative_id}: {gamResult.ErrorMessage}");
                continue;
            }

            if (await _repository.MarkReviewedByCreativeIdAsync(doc.creative_id, cancellationToken))
                result.MarkedReviewed++;
            else
                result.MongoUpdateMissed++;

            result.Processed++;
        }

        Console.WriteLine(
            $"[Blocked TEST] Xong: processed={result.Processed}, is_review=1={result.MarkedReviewed}, " +
            $"mongo_miss={result.MongoUpdateMissed}, errors={result.Errors}");
        return result;
    }
}

public sealed class BlockedPendingBatchResult
{
    public int Found { get; set; }
    public int Processed { get; set; }
    public int MarkedReviewed { get; set; }
    public int MongoUpdateMissed { get; set; }
    public int Errors { get; set; }
}
