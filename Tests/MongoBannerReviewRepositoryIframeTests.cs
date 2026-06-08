using AdOpsAgenReviewBanner.Infrastructure.Mongo;
using Xunit;

namespace AdOpsAgenReviewBanner.Tests;

/// <summary>Kiểm tra contract batch lookup iframe_id (không cần Mongo thật).</summary>
public class MongoBannerReviewRepositoryIframeTests
{
    [Fact]
    public void FindExistingIframeIdsAsync_InterfaceDeclaredOnRepository()
    {
        var method = typeof(MongoBannerReviewRepository).GetMethod(nameof(MongoBannerReviewRepository.FindExistingIframeIdsAsync));
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<HashSet<string>>), method!.ReturnType);
    }
}
