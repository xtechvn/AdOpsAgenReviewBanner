namespace AdOpsAgenReviewBanner.Configuration;

/// <summary>Cấu hình bot Telegram (section "Telegram" trong appsettings.json).</summary>
public sealed class TelegramSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Token bot từ @BotFather. Có thể ghi đè bằng biến môi trường TELEGRAM_BOT_TOKEN.</summary>
    public string BotToken { get; set; } = "";

    /// <summary>Chat ID nhận thông báo (user/group). Bắt buộc để gửi tin.</summary>
    public string ChatId { get; set; } = "";
}
