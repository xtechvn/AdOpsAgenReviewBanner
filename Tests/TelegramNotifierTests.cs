using Xunit;
using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Infrastructure.Telegram;
using AdOpsAgenReviewBanner.Tests.Support;

namespace AdOpsAgenReviewBanner.Tests;

public class TelegramNotifierTests
{
    [Fact]
    public async Task NotifyReviewResultAsync_WhenImageExists_PostsToSendPhoto()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var settings = new TestOptionsMonitor<TelegramSettings>(new TelegramSettings
        {
            Enabled = true,
            BotToken = "test-bot-token",
            ChatId = "123456"
        });

        var notifier = new TelegramNotifier(httpClient, settings);
        var imagePath = Path.Combine(Path.GetTempPath(), $"telegram-test-{Guid.NewGuid():N}.png");

        try
        {
            await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            await notifier.NotifyReviewResultAsync(
                imagePath,
                "Blocked",
                new ReviewTimingMetrics(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(1.2)));

            Assert.Single(handler.Requests);
            var requestUri = handler.Requests[0].RequestUri?.ToString() ?? "";
            Assert.Contains("sendPhoto", requestUri, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("test-bot-token", requestUri, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task NotifyReviewResultAsync_WhenImageMissing_SendsTextOnly()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var settings = new TestOptionsMonitor<TelegramSettings>(new TelegramSettings
        {
            Enabled = true,
            BotToken = "test-bot-token",
            ChatId = "123456"
        });

        var notifier = new TelegramNotifier(httpClient, settings);
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");

        await notifier.NotifyReviewResultAsync(
            missingPath,
            "Reviewed",
            new ReviewTimingMetrics(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Single(handler.Requests);
        var requestUri = handler.Requests[0].RequestUri?.ToString() ?? "";
        Assert.Contains("sendMessage", requestUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sendPhoto", requestUri, StringComparison.OrdinalIgnoreCase);
    }
}
