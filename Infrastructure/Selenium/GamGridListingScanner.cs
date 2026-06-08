using OpenQA.Selenium;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// Quét listing GAM (reviewable-grid) — lấy iframe id từng card trước khi mở preview.
/// </summary>
internal sealed record GridListingCard(string IframeId, IWebElement ClickTarget);

internal static class GamGridListingScanner
{
    public static IReadOnlyList<GridListingCard> ScanListingCards(IWebDriver driver)
    {
        var cards = new List<GridListingCard>();
        IReadOnlyList<IWebElement> rows;

        try
        {
            rows = driver.FindElements(By.XPath(GamReviewSelectors.GridReviewableRows));
        }
        catch (NoSuchElementException)
        {
            return cards;
        }

        foreach (var row in rows)
        {
            try
            {
                if (!row.Displayed)
                    continue;

                var iframeId = TryReadGridIframeId(row);
                var clickTarget = row.FindElement(By.XPath(GamReviewSelectors.GridCardClickTarget));
                if (!clickTarget.Displayed)
                    continue;

                cards.Add(new GridListingCard(iframeId, clickTarget));
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return cards;
    }

    /// <summary>
    /// Ưu tiên iframe darc-ad-preview-div-*_preview_*; fallback iframe bất kỳ trong preview-container.
    /// Card đang load (chưa có iframe) → chuỗi rỗng → coi là banner mới cần mở.
    /// </summary>
    internal static string TryReadGridIframeId(IWebElement reviewableRow)
    {
        foreach (var xpath in GamReviewSelectors.GridPreviewIframeIdCandidates)
        {
            try
            {
                var iframe = reviewableRow.FindElement(By.XPath(xpath));
                var id = iframe.GetDomAttribute("id");
                if (!string.IsNullOrWhiteSpace(id))
                    return id.Trim();
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return "";
    }
}
