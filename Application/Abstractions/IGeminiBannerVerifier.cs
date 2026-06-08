using AdOpsAgenReviewBanner.Domain.Models;

namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Gọi Gemini vision tier 2; không ném exception khi quota/timeout — trả Attempt.</summary>
public interface IGeminiBannerVerifier
{
    Task<GeminiVerifyAttempt> TryVerifyAsync(
        BannerImage image,
        string prompt,
        CancellationToken cancellationToken = default);
}

public sealed record GeminiVerifyAttempt(bool Succeeded, string? RawText, string? ErrorMessage);
