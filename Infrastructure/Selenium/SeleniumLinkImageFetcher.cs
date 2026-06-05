using AdOpsAgenReviewBanner.Application.Abstractions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

public sealed class SeleniumLinkImageFetcher : ILinkImageFetcher
{
    private readonly ITelegramNotifier _telegram;
    private readonly ChromeDriverFactory _chromeDriverFactory;

    public SeleniumLinkImageFetcher(
        ITelegramNotifier telegram,
        ChromeDriverFactory chromeDriverFactory)
    {
        _telegram = telegram;
        _chromeDriverFactory = chromeDriverFactory;
    }

    public async Task<string?> FetchToLocalPathAsync(
        string linkReview,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(linkReview))
            return null;

        ChromeDriver? driver = null;
        try
        {
            driver = _chromeDriverFactory.CreateResilient();
            driver.Navigate().GoToUrl(linkReview);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            if (driver is not ITakesScreenshot takesScreenshot)
                throw new InvalidOperationException("ChromeDriver không hỗ trợ chụp ảnh màn hình.");

            var screenshot = takesScreenshot.GetScreenshot();
            var filePath = Path.Combine(
                Path.GetTempPath(),
                $"banner-{Guid.NewGuid():N}.png");

            screenshot.SaveAsFile(filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(FetchToLocalPathAsync), ex, cancellationToken: cancellationToken);
            throw;
        }
        finally
        {
            try
            {
                driver?.Quit();
            }
            catch
            {
                // no-op
            }

            try
            {
                driver?.Dispose();
            }
            catch
            {
                // no-op
            }
        }
    }
}
