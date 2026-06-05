using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using ReviewBannerUseCase = AdOpsAgenReviewBanner.Application.ReviewBannerUseCase;

namespace AdOpsAgenReviewBanner.Application.Queue;

/// <summary>
/// Xử lý 1 message queue (hoặc TEST với URL):
/// validate mode → Selenium lấy ảnh → ReviewBannerUseCase → xóa file tạm.
/// </summary>

public enum QueueProcessResult
{
    Processed,
    SkippedModeMismatch,
    InvalidMessage,
    FetchImageFailed
}

public sealed class ReviewQueueMessageProcessor
{
    private readonly ILinkImageFetcher _linkImageFetcher;
    private readonly ReviewBannerUseCase _useCase;
    private readonly WorkerMode _workerMode;

    public ReviewQueueMessageProcessor(
        ILinkImageFetcher linkImageFetcher,
        ReviewBannerUseCase useCase,
        WorkerMode workerMode)
    {
        _linkImageFetcher = linkImageFetcher;
        _useCase = useCase;
        _workerMode = workerMode;
    }

    public async Task<QueueProcessResult> ProcessAsync(
        ReviewQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.LinkReview) || string.IsNullOrWhiteSpace(message.Mode))
            return QueueProcessResult.InvalidMessage;

        if (!QueueModeHelper.TryParse(message.Mode, out var messageMode))
            return QueueProcessResult.InvalidMessage;

        if (messageMode != _workerMode)
            return QueueProcessResult.SkippedModeMismatch;

        var tempImagePath = await _linkImageFetcher.FetchToLocalPathAsync(message.LinkReview, cancellationToken);
        if (string.IsNullOrWhiteSpace(tempImagePath))
            return QueueProcessResult.FetchImageFailed;

        try
        {
            var outcome = await _useCase.ExecuteAsync(tempImagePath, cancellationToken);
            Console.WriteLine($"Processed link_review => {outcome.GetType().Name}");
            return QueueProcessResult.Processed;
        }
        finally
        {
            TryDeleteTempFile(tempImagePath);
        }
    }

    private static void TryDeleteTempFile(string tempImagePath)
    {
        try
        {
            if (File.Exists(tempImagePath))
                File.Delete(tempImagePath);
        }
        catch
        {
            // no-op
        }
    }
}
