using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Services;
using AdOpsAgenReviewBanner.Infrastructure.Configuration;
using AdOpsAgenReviewBanner.Infrastructure.Files;
using AdOpsAgenReviewBanner.Infrastructure.Florence;
using AdOpsAgenReviewBanner.Infrastructure.Gemini;
using AdOpsAgenReviewBanner.Infrastructure.Messaging;
using AdOpsAgenReviewBanner.Infrastructure.Mongo;
using AdOpsAgenReviewBanner.Infrastructure.Prompting;
using AdOpsAgenReviewBanner.Infrastructure.Selenium;
using AdOpsAgenReviewBanner.Infrastructure.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdOpsAgenReviewBanner.DependencyInjection;

/// <summary>
/// Đăng ký Dependency Injection (DI): map interface → class triển khai.
/// Gọi một lần từ Program.cs: builder.Services.AddBannerReview(configuration).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBannerReview(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FlorenceSettings>()
            .BindConfiguration("Florence");

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

        services.AddOptions<GamReviewSettings>()
            .BindConfiguration("GamReview");

        services.AddOptions<KeywordApiSettings>()
            .BindConfiguration("KeywordApi");

        services.AddHttpClient<ITelegramNotifier, TelegramNotifier>();
        services.AddHttpClient<HttpKeywordCatalogProvider>();

        var workerMode = WorkerCapability.ResolveWorkerMode(configuration);
        var requiresFlorence = WorkerCapability.RequiresFlorence(workerMode);

        if (requiresFlorence)
        {
            AddFlorenceReviewStack(services);
        }

        AddSeleniumAndQueueStack(services, workerMode);

        services.AddHostedService<RabbitMqReviewConsumerService>();

        return services;
    }

    private static void AddFlorenceReviewStack(IServiceCollection services)
    {
        services.AddSingleton<IVerdictParser, VerdictParser>();
        services.AddSingleton<IReviewPolicyProvider, AppSettingsPolicyProvider>();
        services.AddSingleton<IImageReader, LocalImageReader>();
        services.AddSingleton<IPromptBuilder, BannerReviewPromptBuilder>();

        services.AddSingleton<IOcrTextExtractor, TesseractOcrExtractor>();
        services.AddSingleton<IKeywordCatalogProvider, CompositeKeywordCatalogProvider>();
        services.AddSingleton<BannerKeywordMatcher>();
        services.AddSingleton<IBannerModerationResultHolder, BannerModerationResultHolder>();
        services.AddSingleton<IBannerModerationScanner, FlorenceBannerModerationScanner>();
        services.AddSingleton<IGeminiApiKeyProvider, RandomGeminiApiKeyProvider>();
        services.AddSingleton<GeminiBannerVerifier>();
        services.AddSingleton<IGeminiBannerVerifier>(sp => sp.GetRequiredService<GeminiBannerVerifier>());
        services.AddSingleton<TieredBannerVisionAnalyzer>();
        services.AddSingleton<IBannerVisionAnalyzer>(sp => sp.GetRequiredService<TieredBannerVisionAnalyzer>());

        services.AddSingleton<ReviewBannerUseCase>();
        services.AddSingleton<ReviewBannerBatchRunner>();
        services.AddSingleton<ILinkImageFetcher, SeleniumLinkImageFetcher>();
        services.AddSingleton<IExecutePlanQueuePublisher, RabbitMqExecutePlanQueuePublisher>();
        services.AddSingleton<GamGeneralAdCategoryFilter>();
        services.AddSingleton<IGamAdReviewWorkflow, GamAdReviewCenterWorkflow>();
    }

    private static void AddSeleniumAndQueueStack(IServiceCollection services, WorkerMode workerMode)
    {
        services.AddSingleton<ChromeDriverFactory>();
        services.AddSingleton<IBannerReviewRepository, MongoBannerReviewRepository>();
        services.AddSingleton<IGamBlockedActionWorkflow, GamBlockedActionWorkflow>();
        services.AddSingleton<BlockedPendingReviewRunner>();

        services.AddScoped<ReviewQueueMessageProcessor>(sp =>
        {
            var runtime = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RuntimeSettings>>().Value;
            return new ReviewQueueMessageProcessor(
                runtime.WorkerMode == WorkerMode.Reviewed
                    ? sp.GetRequiredService<IGamAdReviewWorkflow>()
                    : null,
                runtime.WorkerMode == WorkerMode.ExecutePlan
                    ? sp.GetRequiredService<IGamBlockedActionWorkflow>()
                    : null,
                sp.GetRequiredService<IBannerReviewRepository>(),
                runtime.WorkerMode);
        });
    }
}
