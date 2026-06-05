using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Services;
using AdOpsAgenReviewBanner.Infrastructure.Configuration;
using AdOpsAgenReviewBanner.Infrastructure.Files;
using AdOpsAgenReviewBanner.Infrastructure.Gemini;
using AdOpsAgenReviewBanner.Infrastructure.Messaging;
using AdOpsAgenReviewBanner.Infrastructure.Prompting;
using AdOpsAgenReviewBanner.Infrastructure.Selenium;
using AdOpsAgenReviewBanner.Infrastructure.Telegram;
using Microsoft.Extensions.DependencyInjection;

namespace AdOpsAgenReviewBanner.DependencyInjection;

/// <summary>
/// Đăng ký Dependency Injection (DI): map interface → class triển khai.
/// Gọi một lần từ Program.cs: builder.Services.AddBannerReview().
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBannerReview(this IServiceCollection services)
    {
        // --- Đọc cấu hình từ appsettings.json ---
        services.AddOptions<GeminiSettings>()
            .BindConfiguration("Gemini");

        services.AddOptions<BannerReviewSettings>()
            .BindConfiguration("BannerReview");

        services.AddOptions<TelegramSettings>()
            .BindConfiguration("Telegram");

        services.AddOptions<RuntimeSettings>()
            .BindConfiguration("Runtime");

        services.AddOptions<RabbitMqSettings>()
            .BindConfiguration("RabbitMq");

        services.AddOptions<SeleniumSettings>()
            .BindConfiguration("Selenium");

        services.AddOptions<MongoSettings>()
            .BindConfiguration("Mongo");

        services.AddHttpClient<ITelegramNotifier, TelegramNotifier>();

        // --- Domain / Application: lõi review 1 ảnh ---
        services.AddSingleton<IVerdictParser, VerdictParser>();
        services.AddSingleton<IReviewPolicyProvider, AppSettingsPolicyProvider>();
        services.AddSingleton<IImageReader, LocalImageReader>();              // đọc file ảnh local
        services.AddSingleton<IPromptBuilder, BannerReviewPromptBuilder>();
        services.AddSingleton<IGeminiApiKeyProvider, RandomGeminiApiKeyProvider>();

        services.AddSingleton<GeminiVisionAnalyzer>();
        services.AddSingleton<IBannerVisionAnalyzer>(sp => sp.GetRequiredService<GeminiVisionAnalyzer>());

        services.AddSingleton<ReviewBannerUseCase>();       // ← logic chính: ảnh → Gemini → Blocked/Reviewed
        services.AddSingleton<ReviewBannerBatchRunner>();   // quét folder (TEST, không args)

        // --- Queue / Selenium: dùng khi có link_review (Production hoặc TEST URL) ---
        services.AddSingleton<ChromeDriverFactory>();
        services.AddSingleton<ILinkImageFetcher, SeleniumLinkImageFetcher>();
        services.AddScoped<ReviewQueueMessageProcessor>(sp =>
        {
            var runtime = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RuntimeSettings>>().Value;
            return new ReviewQueueMessageProcessor(
                sp.GetRequiredService<ILinkImageFetcher>(),
                sp.GetRequiredService<ReviewBannerUseCase>(),
                runtime.WorkerMode); // strict-filter: chỉ xử lý message cùng mode
        });

        // Chạy nền khi Production; ở Test thì service return ngay, không kết nối RabbitMQ
        services.AddHostedService<RabbitMqReviewConsumerService>();

        return services;
    }
}
