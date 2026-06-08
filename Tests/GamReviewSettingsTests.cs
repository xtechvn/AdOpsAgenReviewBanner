using AdOpsAgenReviewBanner.Configuration;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

public class GamReviewSettingsTests
{
    [Fact]
    public void Default_MinPreview_Is_100x100()
    {
        var settings = new GamReviewSettings();

        Assert.Equal(100, settings.MinPreviewWidth);
        Assert.Equal(100, settings.MinPreviewHeight);
    }

    [Fact]
    public void Default_PreviewDelay_HasSensibleDefaults()
    {
        var settings = new GamReviewSettings();

        Assert.Equal(3, settings.PreviewInitialDelaySeconds);
        Assert.Equal(3, settings.PreviewScreenshotRetryDelaySeconds);
    }

    [Fact]
    public void Default_BetweenBannerDelay_IsLow_ForLocalFlorence()
    {
        Assert.Equal(2, new GamReviewSettings().GeminiDelaySeconds);
    }

    [Fact]
    public void Default_EnableGridPagination_IsTrue()
    {
        Assert.True(new GamReviewSettings().EnableGridPagination);
    }
}
