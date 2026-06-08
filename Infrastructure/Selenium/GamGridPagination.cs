using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

internal enum GridNextPageState
{
    Enabled,
    Disabled,
    NotFound
}

/// <summary>Thao tác phân trang lưới listing GAM (ngoài preview panel).</summary>
internal static class GamGridPagination
{
    private const int CloseLightboxMaxAttempts = 3;
    private static readonly TimeSpan LightboxCloseWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PaginationReadyWait = TimeSpan.FromSeconds(10);

    public static string? TryReadPaginationLabel(IWebDriver driver)
    {
        foreach (var xpath in GamReviewSelectors.GridPaginationLabelCandidates)
        {
            try
            {
                var text = driver.FindElement(By.XPath(xpath)).Text.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return null;
    }

    public static bool IsPreviewPanelOpen(IWebDriver driver)
    {
        try
        {
            var panel = driver.FindElement(By.XPath(GamReviewSelectors.PreviewPanelRoot));
            return panel.Displayed;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    /// <summary>Đóng lightbox: Close ad detail → fallback Escape → chờ về listing.</summary>
    public static bool TryCloseLightboxAndReturnToGrid(IWebDriver driver)
    {
        if (!IsPreviewPanelOpen(driver))
            return WaitForPaginationBarReady(driver);

        var closedByButton = TryClickCloseButton(driver);
        if (closedByButton)
            Console.WriteLine("[Grid] Đóng lightbox: nút Close ad detail.");
        else
            Console.WriteLine("[Grid] Không thấy Close ad detail — dùng phím Escape.");

        if (!WaitForLightboxClosed(driver))
            TryCloseWithEscape(driver);

        if (!WaitForLightboxClosed(driver))
        {
            Console.Error.WriteLine("[Grid] Lightbox vẫn mở sau Close/Escape.");
            return false;
        }

        Console.WriteLine("[Grid] Đã về listing.");
        return WaitForPaginationBarReady(driver);
    }

    public static GridNextPageState GetGridNextPageState(IWebDriver driver)
    {
        var button = TryFindGridNextPageButton(driver);
        if (button is null)
            return GridNextPageState.NotFound;

        if (!button.Displayed)
            return GridNextPageState.NotFound;

        return IsDisabled(button) ? GridNextPageState.Disabled : GridNextPageState.Enabled;
    }

    public static bool TryClickGridNextPage(IWebDriver driver)
    {
        var state = GetGridNextPageState(driver);
        if (state == GridNextPageState.Disabled)
        {
            Console.WriteLine("[Grid] Next page disabled (aria-disabled=true) — hết banner listing.");
            return false;
        }

        if (state == GridNextPageState.NotFound)
        {
            Console.Error.WriteLine("[Grid] Không tìm thấy nút Go to the next page trên pagination-bar.");
            return false;
        }

        var button = TryFindGridNextPageButton(driver);
        if (button is null)
            return false;

        ScrollIntoView(driver, button);

        var labelBefore = TryReadPaginationLabel(driver);
        Console.WriteLine(
            labelBefore is null
                ? "[Grid] Next page enabled — đang chuyển trang..."
                : $"[Grid] Next page enabled ({labelBefore}) — đang chuyển trang...");

        try
        {
            button.Click();
        }
        catch (ElementClickInterceptedException)
        {
            if (driver is IJavaScriptExecutor js)
                js.ExecuteScript("arguments[0].click();", button);
            else
                throw;
        }

        if (!string.IsNullOrWhiteSpace(labelBefore))
            WaitForPaginationLabelChange(driver, labelBefore, TimeSpan.FromSeconds(15));

        var labelAfter = TryReadPaginationLabel(driver);
        if (labelAfter is not null)
            Console.WriteLine($"[Grid] Đã sang trang: {labelAfter}");

        return true;
    }

    private static bool TryClickCloseButton(IWebDriver driver)
    {
        foreach (var xpath in GamReviewSelectors.ClosePreviewPanelCandidates)
        {
            try
            {
                var close = driver.FindElement(By.XPath(xpath));
                if (!close.Displayed || IsDisabled(close))
                    continue;

                ScrollIntoView(driver, close);
                close.Click();
                Thread.Sleep(300);
                return true;
            }
            catch (NoSuchElementException)
            {
            }
            catch (ElementNotInteractableException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return false;
    }

    private static void TryCloseWithEscape(IWebDriver driver)
    {
        for (var attempt = 0; attempt < CloseLightboxMaxAttempts; attempt++)
        {
            try
            {
                new Actions(driver).SendKeys(Keys.Escape).Perform();
            }
            catch
            {
            }

            Thread.Sleep(400);
            if (!IsPreviewPanelOpen(driver))
                return;
        }
    }

    private static bool WaitForLightboxClosed(IWebDriver driver)
    {
        var deadline = DateTime.UtcNow + LightboxCloseWait;
        while (DateTime.UtcNow < deadline)
        {
            if (!IsPreviewPanelOpen(driver))
                return true;

            Thread.Sleep(200);
        }

        return !IsPreviewPanelOpen(driver);
    }

    private static bool WaitForPaginationBarReady(IWebDriver driver)
    {
        var deadline = DateTime.UtcNow + PaginationReadyWait;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var bar = driver.FindElement(By.XPath(GamReviewSelectors.GridPaginationBar));
                if (bar.Displayed)
                    return true;
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }

            Thread.Sleep(200);
        }

        return TryFindGridNextPageButton(driver) is not null;
    }

    private static IWebElement? TryFindGridNextPageButton(IWebDriver driver)
    {
        foreach (var xpath in GamReviewSelectors.GridNextPageButtonCandidates)
        {
            try
            {
                return driver.FindElement(By.XPath(xpath));
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return null;
    }

    private static void WaitForPaginationLabelChange(IWebDriver driver, string labelBefore, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var current = TryReadPaginationLabel(driver);
            if (!string.IsNullOrWhiteSpace(current)
                && !string.Equals(current, labelBefore, StringComparison.Ordinal))
                return;

            Thread.Sleep(200);
        }
    }

    private static void ScrollIntoView(IWebDriver driver, IWebElement element)
    {
        try
        {
            if (driver is IJavaScriptExecutor js)
                js.ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'nearest'});", element);
        }
        catch
        {
        }
    }

    private static bool IsDisabled(IWebElement element)
    {
        var ariaDisabled = element.GetDomAttribute("aria-disabled");
        if (string.Equals(ariaDisabled, "true", StringComparison.OrdinalIgnoreCase))
            return true;

        var disabled = element.GetDomAttribute("disabled");
        return string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase);
    }
}
