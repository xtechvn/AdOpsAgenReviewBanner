using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;

namespace AdOpsAgenReviewBanner.Application.Queue;

/// <summary>
/// Xử lý message queue:
/// - Reviewed: mở link_review → duyệt toàn bộ banner GAM → Florence → Mongo.
/// - ExecutePlan: creative_id + action → thực thi kết quả review trên GAM (Allow/Block).
/// </summary>
public enum QueueProcessResult
{
    Processed,
    SkippedModeMismatch,
    InvalidMessage,
    BlockedActionFailed
}

public sealed class ReviewQueueMessageProcessor
{
    private readonly IGamAdReviewWorkflow? _gamWorkflow;
    private readonly IGamBlockedActionWorkflow? _executePlanWorkflow;
    private readonly IBannerReviewRepository _repository;
    private readonly WorkerMode _workerMode;

    public ReviewQueueMessageProcessor(
        IGamAdReviewWorkflow? gamWorkflow,
        IGamBlockedActionWorkflow? executePlanWorkflow,
        IBannerReviewRepository repository,
        WorkerMode workerMode)
    {
        _gamWorkflow = gamWorkflow;
        _executePlanWorkflow = executePlanWorkflow;
        _repository = repository;
        _workerMode = workerMode;
    }

    public async Task<QueueProcessResult> ProcessAsync(
        ReviewQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.Mode))
            return QueueProcessResult.InvalidMessage;

        if (!QueueModeHelper.TryParse(message.Mode, out var messageMode))
            return QueueProcessResult.InvalidMessage;

        if (messageMode != _workerMode)
            return QueueProcessResult.SkippedModeMismatch;

        if (!message.IsValidForWorker(_workerMode))
            return QueueProcessResult.InvalidMessage;

        if (messageMode == WorkerMode.Reviewed)
        {
            if (_gamWorkflow is null)
                throw new InvalidOperationException("Worker Reviewed cần IGamAdReviewWorkflow (Florence stack).");

            var result = await _gamWorkflow.ProcessReviewListAsync(
                message.LinkReview,
                message.Order!.Value,
                message.Category,
                cancellationToken);
            Console.WriteLine(
                $"GAM workflow done: grid_pages={result.GridPagesProcessed}, processed={result.ProcessedCount}, reviewed={result.ReviewedCount}, skipped={result.SkippedExistingCount}, errors={result.ErrorCount}");
            return QueueProcessResult.Processed;
        }

        if (_executePlanWorkflow is null)
            throw new InvalidOperationException("Worker ExecutePlan cần IGamBlockedActionWorkflow.");

        if (!QueueActionHelper.TryParse(message.Action, out var action))
            return QueueProcessResult.InvalidMessage;

        var executeResult = await _executePlanWorkflow.ApplyActionAsync(
            message.CreativeId,
            action,
            message.LinkReview,
            cancellationToken);

        if (!executeResult.Success)
        {
            Console.Error.WriteLine(
                $"ExecutePlan GAM thất bại creative_id={message.CreativeId}, action={message.Action}: {executeResult.ErrorMessage}");
            return QueueProcessResult.BlockedActionFailed;
        }

        await _repository.MarkReviewedByCreativeIdAsync(message.CreativeId, cancellationToken);

        Console.WriteLine(
            $"ExecutePlan OK creative_id={message.CreativeId}, action={executeResult.ActionLabel}, is_review=1");
        return QueueProcessResult.Processed;
    }
}
