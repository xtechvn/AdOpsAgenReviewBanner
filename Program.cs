// =============================================================================
// FILE CHÍNH (ENTRY POINT) — Ứng dụng bắt đầu từ đây khi chạy / debug.
//
// Luồng tổng quát:
//   1. Đọc appsettings.json (+ appsettings.Production.json nếu có biến môi trường)
//   2. Đăng ký service (DI) trong AddBannerReview()
//   3. Nhánh theo Runtime.Environment:
//        - Test     → chạy local (folder ảnh / 1 file / 1 URL), KHÔNG cần RabbitMQ
//        - Production → chạy nền consumer RabbitMQ (host.RunAsync)
//
// File liên quan khi đọc code:
//   - DependencyInjection/ServiceCollectionExtensions.cs  → gắn class nào với interface nào
//   - Application/ReviewBannerUseCase.cs                  → logic review 1 ảnh (Gemini)
//   - Application/Queue/ReviewQueueMessageProcessor.cs    → xử lý message queue / test URL
//   - Infrastructure/Messaging/RabbitMqReviewConsumerService.cs → nghe RabbitMQ (Production)
//   - Infrastructure/Selenium/SeleniumLinkImageFetcher.cs → mở link GAM, chụp ảnh
//
// DEBUG TEST (Visual Studio / Cursor):
//   - Đặt breakpoint dòng đầu trong khối try bên dưới (sau khi Build host).
//   - appsettings.json: Runtime.Environment = "Test"
//   - F5 hoặc: dotnet run
//   - Hoặc thêm commandLineArgs trong launchSettings (xem Properties/launchSettings.json)
// =============================================================================

using AdOpsAgenReviewBanner.Application;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Queue;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;

// --- Bước 1: Tạo Host (giống "khởi tạo app" trong ASP.NET, nhưng đây là console) ---
var builder = Host.CreateApplicationBuilder(args);

// Worker Blocked trên server: set DOTNET_ENVIRONMENT=ProductionBlocked
var dotnetEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (string.Equals(dotnetEnv, "ProductionBlocked", StringComparison.OrdinalIgnoreCase))
{
    builder.Configuration.AddJsonFile("appsettings.Production.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.Production.Blocked.json", optional: false, reloadOnChange: true);
}

// --- Bước 2: Đăng ký tất cả service (Gemini, Telegram, Selenium, RabbitMQ consumer...) ---
builder.Services.AddBannerReview();

// --- Bước 3: Build container DI — từ đây có thể GetService<T>() ---
using var host = builder.Build();

// Lấy các service cần dùng ngay trong Program (TEST mode)
var batchRunner = host.Services.GetRequiredService<ReviewBannerBatchRunner>();
var useCase = host.Services.GetRequiredService<ReviewBannerUseCase>();
var runtimeSettings = host.Services.GetRequiredService<IOptions<RuntimeSettings>>().Value;

try
{
    // =========================================================================
    // NHÁNH PRODUCTION: chỉ chạy consumer RabbitMQ, không xử lý args dòng lệnh.
    // Message queue → ReviewQueueMessageProcessor → Selenium → ReviewBannerUseCase
    // =========================================================================
    if (runtimeSettings.Environment == RuntimeEnvironment.Production)
    {
        Console.WriteLine(
            $"RabbitMQ consumer started. Worker mode={runtimeSettings.WorkerMode}, environment={runtimeSettings.Environment}");
        // RunAsync giữ app chạy; RabbitMqReviewConsumerService (BackgroundService) tự bật.
        await host.RunAsync();
        return 0;
    }

    // =========================================================================
    // NHÁNH TEST (local): không cần RabbitMQ — debug chủ yếu ở đây.
    // =========================================================================
    Console.WriteLine($"TEST mode. Worker mode={runtimeSettings.WorkerMode}");

    var defaultImageFolder = runtimeSettings.DefaultImageFolder;

    // Không truyền tham số → quét folder mặc định (image_test)
    if (args.Length == 0)
    {
        return await batchRunner.ExecuteFolderAsync(defaultImageFolder);
    }

    var target = args[0];

    // Tham số là URL http(s) → mô phỏng 1 message queue (Selenium + review)
    if (IsHttpUrl(target))
    {
        return await RunTestLinkReviewAsync(host, target, runtimeSettings.WorkerMode);
    }

    // Tham số là folder → quét tất cả ảnh trong folder
    if (IODirectory.Exists(target) || IODirectory.Exists(ReviewBannerBatchRunner.ResolveFolderPath(target)))
    {
        return await batchRunner.ExecuteFolderAsync(target);
    }

    // Tham số là 1 file ảnh → review trực tiếp (nhanh nhất để debug Gemini)
    if (IOFile.Exists(target))
    {
        return MapSingleOutcome(
            await useCase.ExecuteAsync(target),
            target);
    }

    var resolvedFile = ReviewBannerBatchRunner.ResolveFolderPath(target);
    if (IOFile.Exists(resolvedFile))
    {
        return MapSingleOutcome(
            await useCase.ExecuteAsync(resolvedFile),
            resolvedFile);
    }

    Console.Error.WriteLine($"Không tìm thấy file, thư mục hoặc URL hợp lệ: {target}");
    return 1;
}
catch (Exception ex)
{
    await host.Services.GetRequiredService<ITelegramNotifier>()
        .NotifyExceptionAsync("Program", ex, cancellationToken: default);
    Console.Error.WriteLine($"Lỗi không xử lý được: {ex.Message}");
    return 1;
}

static bool IsHttpUrl(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri)
    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

/// <summary>
/// TEST: giả lập 1 message RabbitMQ (link_review + mode) mà không cần broker.
/// Luồng: ReviewQueueMessageProcessor → Selenium chụp ảnh → ReviewBannerUseCase.
/// </summary>
static async Task<int> RunTestLinkReviewAsync(IHost host, string linkReview, WorkerMode workerMode)
{
    using var scope = host.Services.CreateScope();

    var message = new ReviewQueueMessage
    {
        LinkReview = linkReview,
        Mode = workerMode == WorkerMode.Reviewed ? "reviewed" : "blocked"
    };

    Console.WriteLine($"TEST link review: {linkReview}, mode={message.Mode}");

    if (workerMode == WorkerMode.Reviewed)
    {
        var workflow = scope.ServiceProvider.GetRequiredService<IGamAdReviewWorkflow>();
        var gamResult = await workflow.ProcessReviewListAsync(linkReview);
        Console.WriteLine(
            $"GAM workflow: processed={gamResult.ProcessedCount}, reviewed={gamResult.ReviewedCount}, skipped={gamResult.SkippedExistingCount}, errors={gamResult.ErrorCount}");
        return gamResult.ErrorCount == 0 ? 0 : 1;
    }

    var processor = scope.ServiceProvider.GetRequiredService<ReviewQueueMessageProcessor>();
    var result = await processor.ProcessAsync(message);

    return result switch
    {
        QueueProcessResult.Processed => 0,
        QueueProcessResult.SkippedModeMismatch => PrintError("Mode không khớp worker.", 1),
        QueueProcessResult.InvalidMessage => PrintError("Message test không hợp lệ.", 1),
        QueueProcessResult.FetchImageFailed => PrintError("Không tải được ảnh từ link.", 1),
        _ => PrintError("Lỗi không xác định.", 1)
    };
}

static int MapSingleOutcome(ReviewBannerOutcome outcome, string path) =>
    outcome switch
    {
        ReviewBannerOutcome.Success success => PrintSuccess(success),
        ReviewBannerOutcome.FileNotFound notFound => PrintError($"Không tìm thấy file ảnh: {notFound.Path}", 1),
        ReviewBannerOutcome.MissingApiKey => PrintError(
            "Thiếu hoặc hết hạn API key Gemini. Đặt Gemini:ApiKey trong appsettings hoặc GEMINI_API_KEY.", 1),
        ReviewBannerOutcome.InvalidResponse invalid => PrintError(
            $"LLM không trả kết quả hợp lệ. Raw: {invalid.RawText}", 2),
        ReviewBannerOutcome.ApiError api => PrintError($"Lỗi: {api.Message}", 1),
        _ => PrintError("Lỗi không xác định.", 1)
    };

static int PrintSuccess(ReviewBannerOutcome.Success success)
{
    Console.WriteLine(success.Label);
    return 0;
}

static int PrintError(string message, int exitCode)
{
    Console.Error.WriteLine(message);
    return exitCode;
}
