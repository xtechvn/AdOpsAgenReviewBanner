namespace AdOpsAgenReviewBanner.Configuration;

public enum RuntimeEnvironment
{
    Test,
    Production
}

public enum WorkerMode
{
    Reviewed,
    Blocked
}

public sealed class RuntimeSettings
{
    public RuntimeEnvironment Environment { get; set; } = RuntimeEnvironment.Test;
    public WorkerMode WorkerMode { get; set; } = WorkerMode.Reviewed;
    public string DefaultImageFolder { get; set; } = "image_test";
}

public sealed class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;
    public string QueueName { get; set; } = "PROCESS_SETUP_BANNER_DFP_TEST";
    public ushort PrefetchCount { get; set; } = 1;
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
