using AdOpsAgenReviewBanner.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>
/// Phát hiện icon loading dạng cung đen ở góc trên-trái banner (trong ảnh chụp creative).
/// Heuristic: vùng góc chủ yếu nền sáng + có cụm pixel tối nhỏ (spinner Google Shopping).
/// </summary>
public static class BannerTopLeftSpinnerDetector
{
    public static bool HasLoadingSpinner(string imagePath, GamReviewSettings settings)
    {
        if (!File.Exists(imagePath))
            return false;

        try
        {
            using var image = Image.Load<Rgba32>(imagePath);
            return HasLoadingSpinner(image, settings);
        }
        catch
        {
            return false;
        }
    }

    public static bool HasLoadingSpinner(Image<Rgba32> image, GamReviewSettings settings)
    {
        var w = image.Width;
        var h = image.Height;
        if (w < 20 || h < 20)
            return false;

        var regionW = Math.Clamp(
            (int)(w * settings.PreviewSpinnerRegionWidthRatio),
            16,
            settings.PreviewSpinnerRegionMaxPx);
        var regionH = Math.Clamp(
            (int)(h * settings.PreviewSpinnerRegionHeightRatio),
            16,
            settings.PreviewSpinnerRegionMaxPx);

        var darkThreshold = settings.PreviewSpinnerDarkLuminanceMax;
        var minDarkRatio = settings.PreviewSpinnerMinDarkPixelPercent / 100.0;
        var maxDarkRatio = settings.PreviewSpinnerMaxDarkPixelPercent / 100.0;
        var minAvgLum = settings.PreviewSpinnerMinRegionAvgLuminance;

        var darkCount = 0;
        var lumSum = 0L;
        var total = regionW * regionH;

        for (var y = 0; y < regionH; y++)
        {
            for (var x = 0; x < regionW; x++)
            {
                var p = image[x, y];
                var lum = (p.R + p.G + p.B) / 3;
                lumSum += lum;
                if (lum <= darkThreshold)
                    darkCount++;
            }
        }

        var darkRatio = (double)darkCount / total;
        var avgLum = (double)lumSum / total;

        return avgLum >= minAvgLum
            && darkRatio >= minDarkRatio
            && darkRatio <= maxDarkRatio;
    }
}
