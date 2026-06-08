using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// Reviewed: filter bar → General ad category → chọn checkbox item thứ <paramref name="order"/> (1-based) → Apply.
/// </summary>
public sealed class GamGeneralAdCategoryFilter
{
    private readonly GamReviewSettings _settings;

    public GamGeneralAdCategoryFilter(IOptions<GamReviewSettings> settings)
    {
        _settings = settings.Value;
    }

    public void Apply(IWebDriver driver, WebDriverWait wait, int order, CancellationToken cancellationToken)
    {
        if (order < 1)
            throw new ArgumentOutOfRangeException(nameof(order), order, "order phải >= 1.");

        var menuLabel = _settings.CategoryFilterMenuLabel;
        Console.WriteLine($"[Reviewed] Áp dụng filter '{menuLabel}', order={order}...");

        var searchInput = wait.Until(WaitUntilVisible(By.XPath(GamBlockedReviewSelectors.FilterSearchInput)));
        searchInput.Click();
        Thread.Sleep(300);
        cancellationToken.ThrowIfCancellationRequested();

        var menuItem = WaitUntilAnyVisible(
            driver,
            wait,
            GamReviewCategoryFilterSelectors.CategoryMenuItemCandidates(menuLabel));
        menuItem.Click();
        Thread.Sleep(300);
        cancellationToken.ThrowIfCancellationRequested();

        var dialogXPath = GamReviewCategoryFilterSelectors.CategoryDialog(menuLabel);
        wait.Until(WaitUntilVisible(By.XPath(dialogXPath)));

        var treeItems = driver.FindElements(By.XPath(GamReviewCategoryFilterSelectors.CategoryTreeItems(menuLabel)));
        if (treeItems.Count < order)
        {
            throw new InvalidOperationException(
                $"order={order} vượt số category ({treeItems.Count}) trong '{menuLabel}'.");
        }

        var targetItem = treeItems[order - 1];
        var categoryLabel = TryReadCategoryLabel(targetItem);
        ScrollIntoView(driver, targetItem);

        var checkbox = targetItem.FindElement(By.XPath(".//material-checkbox"));
        if (!IsCheckboxChecked(checkbox))
            checkbox.Click();

        Thread.Sleep(200);
        cancellationToken.ThrowIfCancellationRequested();

        var applyButton = wait.Until(WaitUntilVisible(
            By.XPath(GamReviewCategoryFilterSelectors.CategoryApplyButton(menuLabel))));
        applyButton.Click();

        var delaySec = Math.Max(1, _settings.CategoryFilterApplyDelaySeconds);
        Console.WriteLine(
            $"[Reviewed] Đã Apply category order={order}" +
            (categoryLabel is null ? "" : $" ({categoryLabel})") +
            $" — chờ {delaySec}s...");
        Thread.Sleep(TimeSpan.FromSeconds(delaySec));
        cancellationToken.ThrowIfCancellationRequested();

        WaitUntilAnyVisible(driver, wait, GamBlockedReviewSelectors.GridAdCardCandidates);
        Console.WriteLine("[Reviewed] Kết quả lọc category đã hiển thị.");
    }

    private static string? TryReadCategoryLabel(IWebElement treeItem)
    {
        try
        {
            return treeItem.GetDomAttribute("aria-label");
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCheckboxChecked(IWebElement checkbox)
    {
        var ariaChecked = checkbox.GetDomAttribute("aria-checked");
        return string.Equals(ariaChecked, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScrollIntoView(IWebDriver driver, IWebElement element)
    {
        if (driver is not IJavaScriptExecutor js)
            return;

        js.ExecuteScript("arguments[0].scrollIntoView({block:'nearest'});", element);
        Thread.Sleep(150);
    }

    private static IWebElement WaitUntilAnyVisible(IWebDriver driver, WebDriverWait wait, string[] xpaths)
    {
        var deadline = DateTime.UtcNow + wait.Timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var xpath in xpaths)
            {
                try
                {
                    var element = driver.FindElement(By.XPath(xpath));
                    if (element.Displayed)
                        return element;
                }
                catch (NoSuchElementException)
                {
                }
                catch (StaleElementReferenceException)
                {
                }
            }

            Thread.Sleep(200);
        }

        throw new NoSuchElementException($"Không tìm thấy element visible: {string.Join(" | ", xpaths)}");
    }

    private static Func<IWebDriver, IWebElement> WaitUntilVisible(By by) =>
        d =>
        {
            var element = d.FindElement(by);
            return element.Displayed ? element : throw new NoSuchElementException("Element chưa visible.");
        };
}
