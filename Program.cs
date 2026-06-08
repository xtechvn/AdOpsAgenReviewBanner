// =============================================================================
// FILE CHÍNH (ENTRY POINT) — Ứng dụng bắt đầu từ đây khi chạy / debug.
//
// Luồng tổng quát:
//   1. Đọc appsettings.json (một file duy nhất; không dùng appsettings.Production.json)
//   2. Đăng ký service (DI) trong AddBannerReview()
//   3. Nhánh theo Runtime.Environment:
//        - Test     → chạy local (folder ảnh / 1 file / 1 URL), KHÔNG cần RabbitMQ
//        - Production → chạy nền consumer RabbitMQ (host.RunAsync)
//
// File liên quan khi đọc code:
//   - DependencyInjection/ServiceCollectionExtensions.cs  → gắn class nào với interface nào
//   - Application/ReviewBannerUseCase.cs                  → logic review 1 ảnh (Florence-2)
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

// Một file config: bỏ overlay appsettings.Production.json (file cũ trong bin/publish vẫn có thể tồn tại).
for (var i = builder.Configuration.Sources.Count - 1; i >= 0; i--)
{
    if (builder.Configuration.Sources[i] is FileConfigurationSource fileSource
        && fileSource.Path?.EndsWith("appsettings.Production.json", StringComparison.OrdinalIgnoreCase) == true)
    {
        builder.Configuration.Sources.RemoveAt(i);
    }
}

// Production (Reviewed / ExecutePlan): DOTNET_ENVIRONMENT=Production (mặc định khi publish).
// Chọn worker: sửa Runtime:WorkerMode trong appsettings.json (mỗi clone server một file).

// --- Bước 2: Đăng ký service — ExecutePlan không đăng ký Florence (~1 GB) ---
builder.Services.AddBannerReview(builder.Configuration);

// --- Bước 3: Build container DI — từ đây có thể GetService<T>() ---
using var host = builder.Build();

var runtimeSettings = host.Services.GetRequiredService<IOptions<RuntimeSettings>>().Value;

try
{
    // =========================================================================
    // NHÁNH PRODUCTION: chỉ chạy consumer RabbitMQ, không xử lý args dòng lệnh.
    // Message queue → ReviewQueueMessageProcessor → Selenium → ReviewBannerUseCase
    // =========================================================================
    if (runtimeSettings.Environment == RuntimeEnvironment.Production)
    {
        var florenceNote = runtimeSettings.WorkerMode == WorkerMode.ExecutePlan
            ? " (không tải Florence)"
            : " (Florence khi xử lý banner)";
        var seleniumSettings = host.Services.GetRequiredService<IOptions<SeleniumSettings>>().Value;
        var rabbitSettings = host.Services.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
        var consumerQueue = rabbitSettings.ResolveConsumerQueueName(runtimeSettings.WorkerMode);
        Console.WriteLine(
            $"RabbitMQ consumer started. Worker mode={runtimeSettings.WorkerMode}, environment={runtimeSettings.Environment}{florenceNote}");
        Console.WriteLine(
            $"RabbitMQ queue={consumerQueue} (review={rabbitSettings.QueueName}, execute_plan={rabbitSettings.ExecutePlanQueueName})");
        Console.WriteLine(
            $"Selenium config: Headless={seleniumSettings.Headless}, UserDataDirs={seleniumSettings.UserDataDirs}");
        var envWorkerMode = Environment.GetEnvironmentVariable("Runtime__WorkerMode");
        if (!string.IsNullOrWhiteSpace(envWorkerMode))
        {
            Console.WriteLine($"Runtime__WorkerMode từ env: {envWorkerMode} (ghi đè appsettings.json — xóa biến này nếu muốn dùng JSON).");
        }

        if (runtimeSettings.WorkerMode == WorkerMode.Reviewed
            && string.Equals(consumerQueue, rabbitSettings.ExecutePlanQueueName, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                "CẢNH BÁO: Worker Reviewed đang listen queue ExecutePlan — sửa appsettings.json: WorkerMode=Reviewed, QueueName=PROCESS_REVIEW_BANNER_DFP.");
        }

        if (runtimeSettings.WorkerMode == WorkerMode.ExecutePlan
            && !string.Equals(consumerQueue, rabbitSettings.ExecutePlanQueueName, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                "CẢNH BÁO: Worker ExecutePlan phải listen ExecutePlanQueueName — sửa appsettings.json (xem appsettings.ExecutePlan.example.json).");
        }
        // RunAsync giữ app chạy; RabbitMqReviewConsumerService (BackgroundService) tự bật.
        await host.RunAsync();
        return 0;
    }

    // =========================================================================
    // NHÁNH TEST (local): không cần RabbitMQ — debug chủ yếu ở đây.
    // =========================================================================
    Console.WriteLine($"TEST mode. Worker mode={runtimeSettings.WorkerMode}");

    var defaultImageFolder = runtimeSettings.DefaultImageFolder;
    var batchRunner = host.Services.GetService<ReviewBannerBatchRunner>();
    var useCase = host.Services.GetService<ReviewBannerUseCase>();

    // Không truyền tham số → theo WorkerMode / TestStartupMode
    if (args.Length == 0)
    {
        if (runtimeSettings.WorkerMode == WorkerMode.ExecutePlan)
        {
            Console.WriteLine("TEST startup: ExecutePlan worker — quét Mongo is_review=0");
            return await RunBlockedTestFromMongoAsync(host);
        }

        if (runtimeSettings.TestStartupMode == TestStartupMode.GamReview
            && !string.IsNullOrWhiteSpace(runtimeSettings.DefaultGamReviewUrl))
        {
            Console.WriteLine($"TEST startup: GAM Selenium → {runtimeSettings.DefaultGamReviewUrl}");
            return await RunTestLinkReviewAsync(
                host,
                runtimeSettings.DefaultGamReviewUrl,
                runtimeSettings.WorkerMode,
                categoryOrder: null);
        }

        if (batchRunner is null)
        {
            return PrintError(
                "Worker ExecutePlan không hỗ trợ quét folder ảnh (không có Florence). Đổi WorkerMode=Reviewed.",
                1);
        }

        Console.WriteLine($"TEST startup: quét folder → {defaultImageFolder}");
        Console.WriteLine(
            "Không mở Chrome. Để test GAM + Selenium: đặt Runtime:TestStartupMode=GamReview + DefaultGamReviewUrl, " +
            "hoặc chạy: dotnet run -- \"https://admanager.google.com/...\"");
        Console.WriteLine(
            "Cấu hình Chrome: Selenium:Headless=false và Selenium:UserDataDirs (profile đã login GAM).");
        return await batchRunner.ExecuteFolderAsync(defaultImageFolder);
    }

    var target = args[0];

    // Tham số là URL http(s) → mô phỏng 1 message queue (Selenium + review)
    if (IsHttpUrl(target))
    {
        int? categoryOrder = null;
        if (args.Length >= 2 && int.TryParse(args[1], out var parsedOrder))
            categoryOrder = parsedOrder;

        return await RunTestLinkReviewAsync(host, target, runtimeSettings.WorkerMode, categoryOrder);
    }

    // Tham số là folder → quét tất cả ảnh trong folder
    if (IODirectory.Exists(target) || IODirectory.Exists(ReviewBannerBatchRunner.ResolveFolderPath(target)))
    {
        if (batchRunner is null)
            return PrintError("Worker ExecutePlan không hỗ trợ quét folder ảnh.", 1);

        return await batchRunner.ExecuteFolderAsync(target);
    }

    // Tham số là 1 file ảnh → review trực tiếp (nhanh nhất để debug Florence)
    if (IOFile.Exists(target))
    {
        if (useCase is null)
            return PrintError("Worker ExecutePlan không hỗ trợ review file ảnh.", 1);

        return MapSingleOutcome(
            await useCase.ExecuteAsync(target),
            target);
    }

    var resolvedFile = ReviewBannerBatchRunner.ResolveFolderPath(target);
    if (IOFile.Exists(resolvedFile))
    {
        if (useCase is null)
            return PrintError("Worker ExecutePlan không hỗ trợ review file ảnh.", 1);

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
static async Task<int> RunBlockedTestFromMongoAsync(IHost host)
{
    var runner = host.Services.GetRequiredService<BlockedPendingReviewRunner>();
    var result = await runner.RunAsync();
    return result.Errors == 0 ? 0 : 1;
}

static async Task<int> RunTestLinkReviewAsync(
    IHost host,
    string linkReview,
    WorkerMode workerMode,
    int? categoryOrder)
{
    using var scope = host.Services.CreateScope();

    var gamSettings = scope.ServiceProvider.GetRequiredService<IOptions<GamReviewSettings>>().Value;
    var reviewedOrder = categoryOrder ?? gamSettings.DefaultReviewCategoryOrder;

    var message = new ReviewQueueMessage
    {
        LinkReview = linkReview,
        Order = workerMode == WorkerMode.Reviewed ? reviewedOrder : null,
        Mode = workerMode == WorkerMode.Reviewed
            ? QueueModeHelper.ReviewedModeValue
            : QueueModeHelper.ExecutePlanModeValue
    };

    Console.WriteLine(
        workerMode == WorkerMode.Reviewed
            ? $"TEST link review: {linkReview}, mode={message.Mode}, order={reviewedOrder}"
            : $"TEST link review: {linkReview}, mode={message.Mode}");

    if (workerMode == WorkerMode.Reviewed)
    {
        var workflow = scope.ServiceProvider.GetRequiredService<IGamAdReviewWorkflow>();
        var gamResult = await workflow.ProcessReviewListAsync(linkReview, reviewedOrder);
        Console.WriteLine(
            $"GAM workflow: grid_pages={gamResult.GridPagesProcessed}, processed={gamResult.ProcessedCount}, reviewed={gamResult.ReviewedCount}, skipped_existing={gamResult.SkippedExistingCount}, skipped_preview={gamResult.SkippedPreviewCount}, errors={gamResult.ErrorCount}");
        return gamResult.ErrorCount == 0 ? 0 : 1;
    }

    var processor = scope.ServiceProvider.GetRequiredService<ReviewQueueMessageProcessor>();
    var result = await processor.ProcessAsync(message);

    return result switch
    {
        QueueProcessResult.Processed => 0,
        QueueProcessResult.SkippedModeMismatch => PrintError("Mode không khớp worker.", 1),
        QueueProcessResult.InvalidMessage => PrintError("Message test không hợp lệ.", 1),
        QueueProcessResult.BlockedActionFailed => PrintError("Blocked action GAM thất bại.", 1),
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
