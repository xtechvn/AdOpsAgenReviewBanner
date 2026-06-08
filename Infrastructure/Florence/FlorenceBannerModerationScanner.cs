using System.Diagnostics;
using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using AdOpsAgenReviewBanner.Domain.Models;
using Florence2;
using Microsoft.Extensions.Options;
using IOFile = System.IO.File;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Florence-2 ONNX + keyword matcher — clone logic từ CONVERT_IMG_TO_TEXT.</summary>
public sealed class FlorenceBannerModerationScanner : IBannerModerationScanner
{
    private readonly object _modelLock = new();
    private readonly string _modelsRoot;
    private readonly IOcrTextExtractor _ocrExtractor;
    private readonly BannerKeywordMatcher _keywordMatcher;
    private Florence2Model? _florenceModel;

    public FlorenceBannerModerationScanner(
        IOptionsMonitor<FlorenceSettings> settings,
        IOcrTextExtractor ocrExtractor,
        BannerKeywordMatcher keywordMatcher)
    {
        _ocrExtractor = ocrExtractor;
        _keywordMatcher = keywordMatcher;
        _modelsRoot = ResolveModelsPath(settings.CurrentValue.ModelsPath);
    }

    private Florence2Model GetOrLoadModel()
    {
        if (_florenceModel is not null)
            return _florenceModel;

        lock (_modelLock)
        {
            if (_florenceModel is not null)
                return _florenceModel;

            var modelSource = new FlorenceModelDownloader(_modelsRoot);
            if (!modelSource.IsReady)
            {
                Console.WriteLine("Đang tải mô hình Florence-2 (lần đầu có thể mất vài phút)...");
                modelSource.DownloadModelsAsync(status =>
                {
                    if (!string.IsNullOrEmpty(status.Message))
                        Console.WriteLine($"  {status.Message}");
                }).GetAwaiter().GetResult();
            }

            Console.WriteLine("Đang nạp Florence-2 ONNX vào RAM (~1GB)...");
            _florenceModel = new Florence2Model(modelSource);
            Console.WriteLine("Florence-2 sẵn sàng.");
            return _florenceModel;
        }
    }

    public async Task<BannerModerationResult> ScanAsync(
        BannerImage image,
        ReviewPolicy policy,
        CancellationToken cancellationToken = default)
    {
        string? tempPath = null;
        try
        {
            var imagePath = await EnsureLocalImagePathAsync(image, cancellationToken);
            tempPath = imagePath != image.SourcePath ? imagePath : null;

            var tesseractWatch = Stopwatch.StartNew();
            var tesseractOcr = _ocrExtractor.ExtractText(imagePath);
            tesseractWatch.Stop();

            await using var stream = IOFile.OpenRead(imagePath);
            stream.Position = 0;

            var captionWatch = Stopwatch.StartNew();
            var florenceModel = GetOrLoadModel();
            var captionResults = florenceModel.Run(
                TaskTypes.MORE_DETAILED_CAPTION,
                [stream],
                null,
                cancellationToken);
            captionWatch.Stop();

            var aiCaption = captionResults.FirstOrDefault()?.PureText?.Trim() ?? string.Empty;

            stream.Position = 0;
            var ocrWatch = Stopwatch.StartNew();
            var florenceOcrResults = florenceModel.Run(
                TaskTypes.OCR,
                [stream],
                null,
                cancellationToken);
            ocrWatch.Stop();

            Console.WriteLine(
                $"   ↳ Florence ONNX: caption {captionWatch.Elapsed.TotalSeconds:F1}s | OCR {ocrWatch.Elapsed.TotalSeconds:F1}s | Tesseract {tesseractWatch.ElapsedMilliseconds}ms");

            var florenceOcr = florenceOcrResults.FirstOrDefault()?.PureText?.Trim() ?? string.Empty;
            var combinedText = $"{aiCaption} {florenceOcr} {tesseractOcr}".ToLowerInvariant();
            var ocrDisplay = BuildOcrDisplay(tesseractOcr, florenceOcr);

            var blockHits = (await _keywordMatcher.FindBlockKeywordsAsync(combinedText, policy, cancellationToken)).ToList();
            var reviewHits = (await _keywordMatcher.FindReviewKeywordsAsync(combinedText, cancellationToken)).ToList();

            if (blockHits.Count == 0 && _keywordMatcher.HasGamblingContext(combinedText))
                blockHits.Add("game+money/bonus (ngữ cảnh cờ bạc)");

            if (blockHits.Count > 0)
            {
                return new BannerModerationResult
                {
                    Action = BannerModerationAction.Blocked,
                    AiDescription = aiCaption,
                    OcrText = ocrDisplay,
                    MatchedKeywords = blockHits,
                    Reason = $"Phát hiện từ khóa cấm: {string.Join(", ", blockHits)}"
                };
            }

            var weakOcr = WeakOcrHeuristics.IsGenericAiCaption(aiCaption)
                && !WeakOcrHeuristics.HasMeaningfulReadableText($"{florenceOcr} {tesseractOcr}");

            if (reviewHits.Count > 0 || string.IsNullOrWhiteSpace(aiCaption) || weakOcr)
            {
                var reason = reviewHits.Count > 0
                    ? $"Từ khóa cần duyệt: {string.Join(", ", reviewHits)}"
                    : weakOcr
                        ? "Caption AI chung chung và OCR yếu — cần duyệt thủ công"
                        : "AI không mô tả được ảnh — cần duyệt thủ công";

                return new BannerModerationResult
                {
                    Action = BannerModerationAction.NeedsReview,
                    AiDescription = aiCaption,
                    OcrText = ocrDisplay,
                    MatchedKeywords = reviewHits,
                    Reason = reason
                };
            }

            return new BannerModerationResult
            {
                Action = BannerModerationAction.Allowed,
                AiDescription = aiCaption,
                OcrText = ocrDisplay,
                Reason = "Không phát hiện nội dung vi phạm"
            };
        }
        catch (Exception ex)
        {
            return new BannerModerationResult
            {
                Action = BannerModerationAction.NeedsReview,
                ErrorMessage = ex.Message,
                Reason = "Lỗi xử lý Florence-2 — cần duyệt thủ công"
            };
        }
        finally
        {
            if (tempPath is not null && IOFile.Exists(tempPath))
            {
                try { IOFile.Delete(tempPath); }
                catch { /* best effort */ }
            }
        }
    }

    private static async Task<string> EnsureLocalImagePathAsync(BannerImage image, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(image.SourcePath) && IOFile.Exists(image.SourcePath))
            return image.SourcePath;

        var ext = Path.GetExtension(image.SourcePath);
        if (string.IsNullOrEmpty(ext))
            ext = image.MimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                _ => ".png"
            };

        var temp = Path.Combine(Path.GetTempPath(), $"banner-{Guid.NewGuid():N}{ext}");
        await IOFile.WriteAllBytesAsync(temp, image.Bytes, cancellationToken);
        return temp;
    }

    private static string ResolveModelsPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        string[] searchDirs =
        [
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."))
        ];

        foreach (var dir in searchDirs)
        {
            var candidate = Path.Combine(dir, configuredPath);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
    }

    private static string BuildOcrDisplay(string tesseract, string florence)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(tesseract))
            parts.Add($"[Tesseract] {tesseract.Trim()}");
        if (!string.IsNullOrWhiteSpace(florence))
            parts.Add($"[Florence] {florence.Trim()}");
        return string.Join(" | ", parts);
    }
}
