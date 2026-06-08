namespace AdOpsAgenReviewBanner.Configuration;

public enum RuntimeEnvironment
{
    Test,
    Production
}

public enum WorkerMode
{
    Reviewed,
    /// <summary>Thực thi kết quả review (Allow/Block trên GAM) — queue mode <c>execute_plan</c>.</summary>
    ExecutePlan
}

/// <summary>TEST không truyền args: mở GAM (Selenium) hoặc quét folder ảnh.</summary>
public enum TestStartupMode
{
    GamReview,
    ImageFolder
}

public sealed class RuntimeSettings
{
    public RuntimeEnvironment Environment { get; set; } = RuntimeEnvironment.Test;
    public WorkerMode WorkerMode { get; set; } = WorkerMode.Reviewed;
    /// <summary>Khi TestStartupMode = GamReview và không có args → mở URL này bằng Chrome.</summary>
    public TestStartupMode TestStartupMode { get; set; } = TestStartupMode.GamReview;
    public string DefaultGamReviewUrl { get; set; } =
        "https://admanager.google.com/27973503#creatives/ad_review_center";
    public string DefaultImageFolder { get; set; } = "image_test";
}

public sealed class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;
    /// <summary>Queue worker Reviewed consume (mode=reviewed).</summary>
    public string QueueName { get; set; } = "PROCESS_SETUP_BANNER_DFP_TEST";

    /// <summary>Queue publish/consume ExecutePlan (mode=execute_plan).</summary>
    public string ExecutePlanQueueName { get; set; } = "PROCESS_EXECUTE_PLAN_DFP";

    public ushort PrefetchCount { get; set; } = 1;

    /// <summary>Reviewed insert Mongo xong → publish message mode=execute_plan vào ExecutePlanQueueName.</summary>
    public bool PublishExecutePlanAfterMongoInsert { get; set; }

    public string ResolveConsumerQueueName(WorkerMode workerMode) =>
        workerMode == WorkerMode.ExecutePlan ? ExecutePlanQueueName : QueueName;
}

public sealed class SeleniumSettings
{
    public bool Headless { get; set; } = true;
    public int PageLoadTimeoutSeconds { get; set; } = 45;
    public int ImplicitWaitSeconds { get; set; } = 2;
    public string UserDataDirs { get; set; } = "";
    public string ChromeBinaryPath { get; set; } = "";
    public string ChromeDriverDirectory { get; set; } = "";
    public int ChromeSessionMaxRetries { get; set; } = 4;
    public int ChromeSessionRetryMs { get; set; } = 3000;
    public bool ChromeFallbackTempProfile { get; set; } = true;
    public string PageLoadStrategy { get; set; } = "eager";
}
