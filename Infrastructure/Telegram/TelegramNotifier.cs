using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IOFile = System.IO.File;

namespace AdOpsAgenReviewBanner.Infrastructure.Telegram;

/// <summary>Gửi tin nhắn/ảnh qua Telegram Bot API; mỗi public method có try/catch riêng.</summary>
public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<TelegramSettings> _settings;

    public TelegramNotifier(HttpClient httpClient, IOptionsMonitor<TelegramSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task NotifyExceptionAsync(
        string context,
        Exception exception,
        ReviewTimingMetrics? timing = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message =
                $"⚠️ *Exception*\n" +
                $"• Context: `{EscapeMarkdown(context)}`\n" +
                $"• Message: `{EscapeMarkdown(exception.Message)}`" +
                FormatTimingLine(timing);

            await SendTextAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTelegramFailure(nameof(NotifyExceptionAsync), ex);
        }
    }

    public async Task NotifyApiKeyIssueAsync(
        string details,
        ReviewTimingMetrics? timing = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message =
                $"🔑 *API key / quota*\n" +
                $"Gemini API key có thể hết hạn, không hợp lệ hoặc hết quota.\n" +
                $"• Chi tiết: `{EscapeMarkdown(details)}`" +
                FormatTimingLine(timing);

            await SendTextAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTelegramFailure(nameof(NotifyApiKeyIssueAsync), ex);
        }
    }

    public async Task NotifyLlmNoResultAsync(
        string imagePath,
        string? rawResponse,
        ReviewTimingMetrics timing,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = string.IsNullOrWhiteSpace(rawResponse) ? "(empty)" : rawResponse;
            var message =
                $"🤖 *LLM không trả kết quả hợp lệ*\n" +
                $"• Ảnh: `{EscapeMarkdown(imagePath)}`\n" +
                $"• Raw: `{EscapeMarkdown(Truncate(raw, 500))}`\n" +
                $"• {EscapeMarkdown(timing.ToTelegramSummary())}";

            await SendTextAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTelegramFailure(nameof(NotifyLlmNoResultAsync), ex);
        }
    }

    public async Task NotifyReviewResultAsync(
        string imagePath,
        string verdictLabel,
        ReviewTimingMetrics timing,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IOFile.Exists(imagePath))
            {
                await SendTextAsync(
                    $"✅ *Kết quả:* `{EscapeMarkdown(verdictLabel)}`\n" +
                    $"• Ảnh: `{EscapeMarkdown(imagePath)}` (không gửi được file)\n" +
                    $"• {EscapeMarkdown(timing.ToTelegramSummary())}",
                    cancellationToken);
                return;
            }

            var caption =
                $"✅ *Kết quả review banner*\n" +
                $"• Verdict: *{EscapeMarkdown(verdictLabel)}*\n" +
                $"• File: `{EscapeMarkdown(Path.GetFileName(imagePath))}`\n" +
                $"• {EscapeMarkdown(timing.ToTelegramSummary())}";

            await SendPhotoAsync(imagePath, caption, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTelegramFailure(nameof(NotifyReviewResultAsync), ex);
        }
    }

    private static string FormatTimingLine(ReviewTimingMetrics? timing) =>
        timing is null ? "" : $"\n• {timing.ToTelegramSummary()}";

    private async Task SendTextAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetBotConfig(out var token, out var chatId))
                return;

            var url = $"https://api.telegram.org/bot{token}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text,
                parse_mode = "Markdown"
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTelegramFailure(nameof(SendTextAsync), ex);
        }
    }

    private async Task SendPhotoAsync(
        string imagePath,
        string caption,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetBotConfig(out var token, out var chatId))
                return;

            var url = $"https://api.telegram.org/bot{token}/sendPhoto";

            await using var fileStream = IOFile.OpenRead(imagePath);
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(chatId), "chat_id");
            form.Add(new StringContent(caption), "caption");
            form.Add(new StringContent("Markdown"), "parse_mode");

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = GetImageMediaType(imagePath);
            form.Add(fileContent, "photo", Path.GetFileName(imagePath));

            using var response = await _httpClient.PostAsync(url, form, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            LogTelegramFailure(nameof(SendPhotoAsync), ex);
        }
    }

    private bool TryGetBotConfig(out string token, out string chatId)
    {
        var settings = _settings.CurrentValue;

        if (!settings.Enabled)
        {
            token = "";
            chatId = "";
            return false;
        }

        token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? settings.BotToken;
        chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? settings.ChatId;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            Console.Error.WriteLine(
                "[Telegram] Thiếu BotToken hoặc ChatId. Cấu hình section Telegram hoặc TELEGRAM_BOT_TOKEN / TELEGRAM_CHAT_ID.");
            return false;
        }

        return true;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Telegram API {(int)response.StatusCode}: {Truncate(body, 300)}");
    }

    private static MediaTypeHeaderValue GetImageMediaType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => new MediaTypeHeaderValue("image/png"),
            ".jpg" or ".jpeg" => new MediaTypeHeaderValue("image/jpeg"),
            ".webp" => new MediaTypeHeaderValue("image/webp"),
            _ => new MediaTypeHeaderValue("application/octet-stream")
        };

    private static string EscapeMarkdown(string value) =>
        value.Replace("\\", "\\\\").Replace("`", "\\`").Replace("*", "\\*");

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static void LogTelegramFailure(string method, Exception ex) =>
        Console.Error.WriteLine($"[Telegram] {method} failed: {ex.Message}");
}
