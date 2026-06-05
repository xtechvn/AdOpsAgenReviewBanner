using System.Diagnostics;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Application.Helpers;
using AdOpsAgenReviewBanner.Domain;
using AdOpsAgenReviewBanner.Domain.Models;
using AdOpsAgenReviewBanner.Domain.Services;

namespace AdOpsAgenReviewBanner.Application;

/// <summary>
/// Use case chính: review một banner và trả Blocked/Reviewed; gửi thông báo Telegram theo từng tình huống.
/// </summary>
public sealed class ReviewBannerUseCase
{
    private readonly IReviewPolicyProvider _policyProvider;
    private readonly IImageReader _imageReader;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IBannerVisionAnalyzer _visionAnalyzer;
    private readonly IVerdictParser _verdictParser;
    private readonly ITelegramNotifier _telegram;

    public ReviewBannerUseCase(
        IReviewPolicyProvider policyProvider,
        IImageReader imageReader,
        IPromptBuilder promptBuilder,
        IBannerVisionAnalyzer visionAnalyzer,
        IVerdictParser verdictParser,
        ITelegramNotifier telegram)
    {
        _policyProvider = policyProvider;
        _imageReader = imageReader;
        _promptBuilder = promptBuilder;
        _visionAnalyzer = visionAnalyzer;
        _verdictParser = verdictParser;
        _telegram = telegram;
    }

    public async Task<ReviewBannerOutcome> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var imageRead = TimeSpan.Zero;
        var policyLoad = TimeSpan.Zero;
        var promptBuild = TimeSpan.Zero;
        var llmAnalyze = TimeSpan.Zero;

        ReviewTimingMetrics Timing() =>
            new(imageRead, policyLoad, promptBuild, llmAnalyze);

        BannerImage? image;
        var readStopwatch = Stopwatch.StartNew();
        try
        {
            image = await _imageReader.TryReadAsync(imagePath, cancellationToken);
        }
        catch (Exception ex)
        {
            readStopwatch.Stop();
            imageRead = readStopwatch.Elapsed;
            await _telegram.NotifyExceptionAsync(nameof(IImageReader.TryReadAsync), ex, Timing(), cancellationToken);
            return new ReviewBannerOutcome.ApiError(ex.Message);
        }

        readStopwatch.Stop();
        imageRead = readStopwatch.Elapsed;

        if (image is null)
        {
            try
            {
                await _telegram.NotifyExceptionAsync(
                    "LoadImage",
                    new FileNotFoundException($"Không tìm thấy file ảnh: {imagePath}"),
                    Timing(),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Notify] {ex.Message}");
            }

            return new ReviewBannerOutcome.FileNotFound(imagePath);
        }

        try
        {
            ReviewPolicy policy;
            var policyStopwatch = Stopwatch.StartNew();
            try
            {
                policy = await _policyProvider.GetPolicyAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                policyStopwatch.Stop();
                policyLoad = policyStopwatch.Elapsed;
                await _telegram.NotifyExceptionAsync(nameof(IReviewPolicyProvider.GetPolicyAsync), ex, Timing(), cancellationToken);
                return new ReviewBannerOutcome.ApiError(ex.Message);
            }

            policyStopwatch.Stop();
            policyLoad = policyStopwatch.Elapsed;

            string prompt;
            var promptStopwatch = Stopwatch.StartNew();
            try
            {
                prompt = _promptBuilder.Build(policy);
            }
            catch (Exception ex)
            {
                promptStopwatch.Stop();
                promptBuild = promptStopwatch.Elapsed;
                await _telegram.NotifyExceptionAsync(nameof(IPromptBuilder.Build), ex, Timing(), cancellationToken);
                return new ReviewBannerOutcome.ApiError(ex.Message);
            }

            promptStopwatch.Stop();
            promptBuild = promptStopwatch.Elapsed;

            string? rawText;
            var llmStopwatch = Stopwatch.StartNew();
            try
            {
                rawText = await _visionAnalyzer.AnalyzeAsync(image, prompt, cancellationToken);
            }
            catch (Exception ex)
            {
                llmStopwatch.Stop();
                llmAnalyze = llmStopwatch.Elapsed;
                await _telegram.NotifyExceptionAsync(nameof(IBannerVisionAnalyzer.AnalyzeAsync), ex, Timing(), cancellationToken);

                if (ApiKeyErrorDetector.IsApiKeyOrQuotaIssue(ex.Message))
                    await _telegram.NotifyApiKeyIssueAsync(ex.Message, Timing(), cancellationToken);

                return new ReviewBannerOutcome.ApiError(ex.Message);
            }

            llmStopwatch.Stop();
            llmAnalyze = llmStopwatch.Elapsed;

            if (_verdictParser.Parse(rawText, policy) is not BannerVerdictKind verdict)
            {
                await _telegram.NotifyLlmNoResultAsync(image.SourcePath, rawText, Timing(), cancellationToken);
                return new ReviewBannerOutcome.InvalidResponse(rawText);
            }

            var label = verdict == BannerVerdictKind.Blocked
                ? policy.BlockedLabel
                : policy.ReviewedLabel;

            await _telegram.NotifyReviewResultAsync(image.SourcePath, label, Timing(), cancellationToken);

            return new ReviewBannerOutcome.Success(verdict, label);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
        {
            await _telegram.NotifyApiKeyIssueAsync(ex.Message, Timing(), cancellationToken);
            return new ReviewBannerOutcome.MissingApiKey();
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(ExecuteAsync), ex, Timing(), cancellationToken);

            if (ApiKeyErrorDetector.IsApiKeyOrQuotaIssue(ex.Message))
                await _telegram.NotifyApiKeyIssueAsync(ex.Message, Timing(), cancellationToken);

            return new ReviewBannerOutcome.ApiError(ex.Message);
        }
    }
}
