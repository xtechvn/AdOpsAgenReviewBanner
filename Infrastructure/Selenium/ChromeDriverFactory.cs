using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

public sealed class ChromeDriverFactory
{
    private static readonly object ProfileRandomLock = new();
    private static readonly Random ProfileRandom = new();

    private readonly SeleniumSettings _settings;

    public ChromeDriverFactory(IOptions<SeleniumSettings> settings)
    {
        _settings = settings.Value;
    }

    public ChromeDriver CreateResilient()
    {
        var primaryProfile = PickRandomUserDataDir(_settings.UserDataDirs);
        var maxRetries = Math.Max(1, _settings.ChromeSessionMaxRetries);
        var retryMs = Math.Max(500, _settings.ChromeSessionRetryMs);
        Exception? last = null;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine(
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Chrome session attempt {attempt + 1}/{maxRetries} (profile: {primaryProfile})");
                return CreateDriver(primaryProfile);
            }
            catch (Exception ex)
            {
                last = ex;
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Chrome start failed: {ex.Message}");
                if (attempt < maxRetries - 1)
                    Thread.Sleep(retryMs);
            }
        }

        if (_settings.ChromeFallbackTempProfile)
        {
            try
            {
                var temp = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AdOpsAgenReviewBanner",
                    "chrome_session_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Chrome fallback temp profile: {temp}");
                return CreateDriver(temp);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            "Không tạo được phiên Chrome/WebDriver sau nhiều lần thử.",
            last);
    }

    private ChromeDriver CreateDriver(string profileDir)
    {
        var options = BuildChromeOptions(profileDir);
        var service = BuildChromeDriverService();
        var driver = new ChromeDriver(service, options, TimeSpan.FromMinutes(3));
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(_settings.PageLoadTimeoutSeconds);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(_settings.ImplicitWaitSeconds);
        return driver;
    }

    private ChromeOptions BuildChromeOptions(string profileDir)
    {
        var options = new ChromeOptions();

        if (!string.IsNullOrWhiteSpace(_settings.ChromeBinaryPath) && File.Exists(_settings.ChromeBinaryPath))
            options.BinaryLocation = _settings.ChromeBinaryPath;

        options.AddArgument("--start-maximized");
        if (!string.IsNullOrWhiteSpace(profileDir))
            options.AddArgument("--user-data-dir=" + profileDir.Trim());

        if (_settings.Headless)
        {
            options.AddArgument("--headless=new");
            options.AddArgument("--window-size=1920,1080");
        }

        options.AddArgument("--disable-extensions");
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);

        var strategy = (_settings.PageLoadStrategy ?? "eager").Trim().ToLowerInvariant();
        options.PageLoadStrategy = strategy switch
        {
            "none" => PageLoadStrategy.None,
            "normal" => PageLoadStrategy.Normal,
            _ => PageLoadStrategy.Eager
        };

        return options;
    }

    private ChromeDriverService BuildChromeDriverService()
    {
        var driverDir = (_settings.ChromeDriverDirectory ?? "").Trim();
        var service = !string.IsNullOrEmpty(driverDir) && Directory.Exists(driverDir)
            ? ChromeDriverService.CreateDefaultService(driverDir)
            : ChromeDriverService.CreateDefaultService();

        service.HideCommandPromptWindow = true;
        return service;
    }

    private static string PickRandomUserDataDir(string userDataDirConfig)
    {
        if (string.IsNullOrWhiteSpace(userDataDirConfig))
            return "";

        var dirs = userDataDirConfig
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dirs.Length == 0)
            return "";
        if (dirs.Length == 1)
            return dirs[0];

        lock (ProfileRandomLock)
            return dirs[ProfileRandom.Next(dirs.Length)];
    }
}
