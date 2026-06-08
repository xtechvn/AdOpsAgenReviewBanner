using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Helpers;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Microsoft.Extensions.Options;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>
/// Pipeline hai tầng: Florence tier 1 → Gemini tier 2 khi Blocked/NeedsReview.
/// Allowed → Reviewed ngay; lỗi quota/timeout Gemini → fallback Florence.
/// </summary>
public sealed class TieredBannerVisionAnalyzer : IBannerVisionAnalyzer
{
    private readonly IBannerModerationScanner _scanner;
    private readonly IBannerModerationResultHolder _resultHolder;
    private readonly IReviewPolicyProvider _policyProvider;
    private readonly IGeminiBannerVerifier _geminiVerifier;
    private readonly IOptionsMonitor<BannerReviewSettings> _bannerReviewSettings;
    private readonly IOptionsMonitor<GeminiSettings> _geminiSettings;
    private readonly ITelegramNotifier _telegram;

    public TieredBannerVisionAnalyzer(
        IBannerModerationScanner scanner,
        IBannerModerationResultHolder resultHolder,
        IReviewPolicyProvider policyProvider,
        IGeminiBannerVerifier geminiVerifier,
        IOptionsMonitor<BannerReviewSettings> bannerReviewSettings,
        IOptionsMonitor<GeminiSettings> geminiSettings,
        ITelegramNotifier telegram)
    {
        _scanner = scanner;
        _resultHolder = resultHolder;
        _policyProvider = policyProvider;
        _geminiVerifier = geminiVerifier;
        _bannerReviewSettings = bannerReviewSettings;
        _geminiSettings = geminiSettings;
        _telegram = telegram;
    }

    public async Task<string?> AnalyzeAsync(
        BannerImage image,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await _policyProvider.GetPolicyAsync(cancellationToken);
            var florence = await _scanner.ScanAsync(image, policy, cancellationToken);
            var labels = _bannerReviewSettings.CurrentValue;

            if (florence.Action == BannerModerationAction.Allowed)
            {
                _resultHolder.SetLastResult(Enrich(florence, geminiAttempted: false, finalSource: ModerationFinalSource.FlorenceOnly));
                return labels.ReviewedLabel;
            }

            if (!_geminiSettings.CurrentValue.Enabled)
            {
                var florenceOnly = Enrich(florence, geminiAttempted: false, finalSource: ModerationFinalSource.FlorenceOnly);
                _resultHolder.SetLastResult(florenceOnly);
                return FlorenceVerdictMapper.ToLabel(florence.Action, labels);
            }

            var geminiAttempt = await _geminiVerifier.TryVerifyAsync(image, prompt, cancellationToken);
            if (geminiAttempt.Succeeded && !string.IsNullOrWhiteSpace(geminiAttempt.RawText))
            {
                _resultHolder.SetLastResult(Enrich(
                    florence,
                    geminiAttempted: true,
                    geminiVerdict: geminiAttempt.RawText,
                    finalSource: ModerationFinalSource.Gemini));
                return geminiAttempt.RawText;
            }

            if (!GeminiFallbackDetector.ShouldFallback(geminiAttempt.ErrorMessage))
            {
                await _telegram.NotifyExceptionAsync(
                    nameof(IGeminiBannerVerifier.TryVerifyAsync),
                    new InvalidOperationException(geminiAttempt.ErrorMessage ?? "Gemini verify failed."),
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException(geminiAttempt.ErrorMessage ?? "Gemini verify failed.");
            }

            Console.WriteLine($"   ↳ Gemini fallback → Florence: {geminiAttempt.ErrorMessage}");
            _resultHolder.SetLastResult(Enrich(
                florence,
                geminiAttempted: true,
                geminiError: geminiAttempt.ErrorMessage,
                finalSource: ModerationFinalSource.FlorenceFallback));
            return FlorenceVerdictMapper.ToLabel(florence.Action, labels);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _telegram.NotifyExceptionAsync(nameof(AnalyzeAsync), ex, cancellationToken: cancellationToken);
            throw;
        }
    }

    private static BannerModerationResult Enrich(
        BannerModerationResult florence,
        bool geminiAttempted,
        string? geminiVerdict = null,
        string? geminiError = null,
        string finalSource = ModerationFinalSource.FlorenceOnly) =>
        new()
        {
            Action = florence.Action,
            FlorenceAction = florence.Action,
            AiDescription = florence.AiDescription,
            OcrText = florence.OcrText,
            MatchedKeywords = florence.MatchedKeywords,
            Reason = florence.Reason,
            ErrorMessage = florence.ErrorMessage,
            GeminiAttempted = geminiAttempted,
            GeminiVerdict = geminiVerdict,
            GeminiError = geminiError,
            FinalSource = finalSource
        };
}
