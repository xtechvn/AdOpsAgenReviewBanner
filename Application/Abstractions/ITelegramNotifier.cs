namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Gửi thông báo vận hành qua Telegram Bot API.</summary>
public interface ITelegramNotifier
{
    Task NotifyExceptionAsync(
        string context,
        Exception exception,
        ReviewTimingMetrics? timing = null,
        CancellationToken cancellationToken = default);

    Task NotifyApiKeyIssueAsync(
        string details,
        ReviewTimingMetrics? timing = null,
        CancellationToken cancellationToken = default);

    Task NotifyLlmNoResultAsync(
        string imagePath,
        string? rawResponse,
        ReviewTimingMetrics timing,
        CancellationToken cancellationToken = default);

    Task NotifyReviewResultAsync(
        string imagePath,
        string verdictLabel,
        ReviewTimingMetrics timing,
        CancellationToken cancellationToken = default);
}
