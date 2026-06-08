using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Configuration;
using Microsoft.Extensions.Options;
using Tesseract;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>OCR bổ sung Tesseract — ưu tiên vie+eng cho banner tiếng Việt.</summary>
public sealed class TesseractOcrExtractor : IOcrTextExtractor
{
    private readonly IOptionsMonitor<FlorenceSettings> _settings;

    public TesseractOcrExtractor(IOptionsMonitor<FlorenceSettings> settings)
    {
        _settings = settings;
    }

    public string ExtractText(string imagePath)
    {
        var settings = _settings.CurrentValue;
        if (!settings.EnableTesseract)
            return string.Empty;

        var tessDataPath = ResolveTessDataPath(settings.TessDataPath);
        if (!Directory.Exists(tessDataPath))
        {
            Console.Error.WriteLine($"[Tesseract] Thiếu thư mục tessdata: {tessDataPath}");
            return string.Empty;
        }

        var languages = ResolveLanguages(settings.TesseractLanguages, tessDataPath);
        if (languages.Count == 0)
        {
            Console.Error.WriteLine("[Tesseract] Không có file .traineddata phù hợp (cần vie.traineddata và/hoặc eng.traineddata).");
            return string.Empty;
        }

        string? bestText = null;
        var bestScore = 0;

        foreach (var language in languages)
        {
            foreach (var mode in new[] { PageSegMode.SparseText, PageSegMode.Auto, PageSegMode.SingleBlock })
            {
                var text = TryExtract(imagePath, tessDataPath, language, mode);
                var score = ScoreText(text);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestText = text;
                }
            }
        }

        return bestText?.Trim() ?? string.Empty;
    }

    private static string TryExtract(string imagePath, string tessDataPath, string language, PageSegMode mode)
    {
        try
        {
            using var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
            engine.SetVariable("preserve_interword_spaces", "1");
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img, mode);
            return page.GetText()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int ScoreText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var letters = text.Count(char.IsLetter);
        var digits = text.Count(char.IsDigit);
        return letters + digits / 2;
    }

    private static List<string> ResolveLanguages(string configured, string tessDataPath)
    {
        var requested = configured
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .DefaultIfEmpty("eng")
            .ToList();

        var available = requested
            .Where(lang => File.Exists(Path.Combine(tessDataPath, $"{lang}.traineddata")))
            .ToList();

        if (available.Count > 0)
            return available;

        if (File.Exists(Path.Combine(tessDataPath, "eng.traineddata")))
            return ["eng"];

        return [];
    }

    private static string ResolveTessDataPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath) && Directory.Exists(configuredPath))
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
}
