using System.Globalization;
using System.Text.RegularExpressions;
using AdOpsAgenReviewBanner.Configuration;
using OpenQA.Selenium;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

internal enum PreviewSkipReason
{
    MissingPreview,
    Undersized,
    NoIframe,
    SpinnerLoading
}

internal readonly record struct PreviewDimensions(int Width, int Height, string Source);

/// <summary>Kiểm tra preview GAM có đủ kích thước / có nội dung trước khi chụp ảnh.</summary>
internal static class GamPreviewInspector
{
    private static readonly Regex IframeSizeFromIdRegex = new(
        @"_v\d+_(\d+)_(\d+)_",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static PreviewSkipReason? GetSkipReason(IWebDriver driver, GamReviewSettings settings)
    {
        var dims = TryGetRenderedPreviewDimensions(driver);
        if (dims is null)
            return PreviewSkipReason.NoIframe;

        if (dims.Value.Width < settings.MinPreviewWidth || dims.Value.Height < settings.MinPreviewHeight)
            return PreviewSkipReason.Undersized;

        return null;
    }

    /// <summary>Chỉ iframe — dùng khi poll ổn định (tránh container-size nhảy 752↔300 reset poll).</summary>
    public static PreviewDimensions? TryGetIframeOnlyDimensions(IWebDriver driver)
    {
        PreviewDimensions? best = null;

        foreach (var xpath in GamReviewSelectors.PreviewIframeIdCandidates)
        {
            try
            {
                var iframe = driver.FindElement(By.XPath(xpath));
                if (!iframe.Displayed)
                    continue;

                best = PickLarger(
                    best,
                    PickBestRendered(
                        iframe.Size,
                        TryParseDimensionsFromIframeId(iframe.GetDomAttribute("id")),
                        TryParseDimensionsFromAttributes(
                            iframe.GetDomAttribute("width"),
                            iframe.GetDomAttribute("height"))));
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return best;
    }

    /// <summary>Kích thước preview thực tế — lấy max của iframe render + khung creative-preview/container.</summary>
    public static PreviewDimensions? TryGetRenderedPreviewDimensions(IWebDriver driver)
    {
        var iframeDims = TryGetIframeOnlyDimensions(driver);
        PreviewDimensions? best = iframeDims;

        foreach (var xpath in GamReviewSelectors.AdPreviewScreenshotCandidates)
        {
            try
            {
                var container = driver.FindElement(By.XPath(xpath));
                if (!container.Displayed)
                    continue;

                var size = container.Size;
                if (size.Width > 0 && size.Height > 0)
                {
                    best = PickLarger(best, new PreviewDimensions(size.Width, size.Height, "container-size"));
                }
            }
            catch (NoSuchElementException)
            {
            }
            catch (StaleElementReferenceException)
            {
            }
        }

        return best;
    }

    /// <inheritdoc cref="TryGetRenderedPreviewDimensions"/>
    public static PreviewDimensions? TryGetRenderedIframeDimensions(IWebDriver driver) =>
        TryGetRenderedPreviewDimensions(driver);

    public static string FormatSkipMessage(
        PreviewSkipReason reason,
        string creativeId,
        PreviewDimensions? dims,
        GamReviewSettings settings) =>
        reason switch
        {
            PreviewSkipReason.MissingPreview =>
                $"creative_id={creativeId} — không có nội dung preview (bỏ qua)",
            PreviewSkipReason.Undersized when dims is not null =>
                $"creative_id={creativeId} — preview {dims.Value.Width}x{dims.Value.Height} ({dims.Value.Source}) < {settings.MinPreviewWidth}x{settings.MinPreviewHeight} (bỏ qua)",
            PreviewSkipReason.Undersized =>
                $"creative_id={creativeId} — preview quá nhỏ (bỏ qua)",
            PreviewSkipReason.NoIframe =>
                $"creative_id={creativeId} — không tìm thấy iframe preview (bỏ qua)",
            PreviewSkipReason.SpinnerLoading =>
                $"creative_id={creativeId} — material-spinner vẫn load (bỏ qua, tránh chụp ảnh trắng)",
            _ => $"creative_id={creativeId} — bỏ qua preview"
        };

    private static PreviewDimensions? PickLarger(PreviewDimensions? left, PreviewDimensions? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;

        return left.Value.Width * left.Value.Height >= right.Value.Width * right.Value.Height
            ? left
            : right;
    }

    private static PreviewDimensions? PickBestRendered(
        System.Drawing.Size elementSize,
        PreviewDimensions? fromId,
        PreviewDimensions? fromAttr)
    {
        var fromElement = elementSize is { Width: > 0, Height: > 0 }
            ? new PreviewDimensions(elementSize.Width, elementSize.Height, "rendered-size")
            : (PreviewDimensions?)null;

        PreviewDimensions? best = null;
        foreach (var candidate in new[] { fromElement, fromId, fromAttr })
        {
            if (candidate is null || candidate.Value.Width <= 0 || candidate.Value.Height <= 0)
                continue;

            if (best is null
                || candidate.Value.Width * candidate.Value.Height > best.Value.Width * best.Value.Height)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static PreviewDimensions? TryParseDimensionsFromIframeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var match = IframeSizeFromIdRegex.Match(id);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return null;
        }

        return new PreviewDimensions(width, height, "iframe-id");
    }

    private static PreviewDimensions? TryParseDimensionsFromAttributes(string? widthRaw, string? heightRaw)
    {
        if (!int.TryParse(widthRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(heightRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return null;
        }

        return new PreviewDimensions(width, height, "iframe-attr");
    }
}
