using AdOpsAgenReviewBanner.Domain;

namespace AdOpsAgenReviewBanner.Application;

/// <summary>Kết quả use case — thay cho throw exception hoặc exit code rải rác trong Program.</summary>
public abstract record ReviewBannerOutcome
{
    public sealed record Success(BannerVerdictKind Verdict, string Label) : ReviewBannerOutcome;

    public sealed record FileNotFound(string Path) : ReviewBannerOutcome;

    public sealed record MissingApiKey : ReviewBannerOutcome;

    public sealed record InvalidResponse(string? RawText) : ReviewBannerOutcome;

    public sealed record ApiError(string Message) : ReviewBannerOutcome;
}
