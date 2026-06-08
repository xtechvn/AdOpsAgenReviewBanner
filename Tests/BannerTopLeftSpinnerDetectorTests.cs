using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Infrastructure.Selenium;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class BannerTopLeftSpinnerDetectorTests
{
    private static GamReviewSettings DefaultSettings() => new();

    [Fact]
    public void HasLoadingSpinner_PlainWhite_ReturnsFalse()
    {
        using var image = new Image<Rgba32>(300, 250);
        image.Mutate(ctx => ctx.BackgroundColor(SixLabors.ImageSharp.Color.White));

        Assert.False(BannerTopLeftSpinnerDetector.HasLoadingSpinner(image, DefaultSettings()));
    }

    [Fact]
    public void HasLoadingSpinner_DarkArcInTopLeft_ReturnsTrue()
    {
        using var image = new Image<Rgba32>(300, 250);
        image.Mutate(ctx => ctx.BackgroundColor(SixLabors.ImageSharp.Color.White));

        // Mô phỏng cung spinner đen góc trên-trái
        for (var y = 8; y < 36; y++)
        for (var x = 8; x < 36; x++)
        {
            if (x + y is > 20 and < 48)
                image[x, y] = new Rgba32(30, 30, 30);
        }

        Assert.True(BannerTopLeftSpinnerDetector.HasLoadingSpinner(image, DefaultSettings()));
    }

    [Fact]
    public void HasLoadingSpinner_DarkBottomRight_ReturnsFalse()
    {
        using var image = new Image<Rgba32>(300, 250);
        image.Mutate(ctx => ctx.BackgroundColor(SixLabors.ImageSharp.Color.White));

        for (var y = 200; y < 230; y++)
        for (var x = 250; x < 280; x++)
            image[x, y] = new Rgba32(30, 30, 30);

        Assert.False(BannerTopLeftSpinnerDetector.HasLoadingSpinner(image, DefaultSettings()));
    }
}
