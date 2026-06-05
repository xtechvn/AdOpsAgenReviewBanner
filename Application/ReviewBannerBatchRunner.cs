namespace AdOpsAgenReviewBanner.Application;

/// <summary>Quét folder ảnh và chạy review tuần tự từng file.</summary>
public sealed class ReviewBannerBatchRunner
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".heic", ".heif"];

    private readonly ReviewBannerUseCase _useCase;

    public ReviewBannerBatchRunner(ReviewBannerUseCase useCase)
    {
        _useCase = useCase;
    }

    public async Task<int> ExecuteFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        var resolvedFolder = ResolveFolderPath(folderPath);
        if (!Directory.Exists(resolvedFolder))
        {
            Console.Error.WriteLine($"Không tìm thấy thư mục: {resolvedFolder}");
            return 1;
        }

        var imageFiles = Directory
            .EnumerateFiles(resolvedFolder)
            .Where(IsSupportedImage)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imageFiles.Count == 0)
        {
            Console.Error.WriteLine($"Không có ảnh trong thư mục: {resolvedFolder}");
            return 1;
        }

        Console.WriteLine($"Quét {imageFiles.Count} ảnh trong: {resolvedFolder}");
        Console.WriteLine(new string('-', 60));

        var exitCode = 0;
        var index = 0;

        foreach (var imagePath in imageFiles)
        {
            index++;
            Console.WriteLine($"[{index}/{imageFiles.Count}] {Path.GetFileName(imagePath)}");

            var outcome = await _useCase.ExecuteAsync(imagePath, cancellationToken);
            var code = MapOutcome(outcome, imagePath);

            if (code != 0)
                exitCode = code;

            Console.WriteLine(new string('-', 60));
        }

        Console.WriteLine($"Hoàn tất {imageFiles.Count} ảnh. Exit code: {exitCode}");
        return exitCode;
    }

    public static string ResolveFolderPath(string folderPath)
    {
        if (Path.IsPathRooted(folderPath))
            return folderPath;

        string[] searchDirs =
        [
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."))
        ];

        return searchDirs
            .Select(dir => Path.Combine(dir, folderPath))
            .FirstOrDefault(Directory.Exists)
            ?? Path.Combine(Directory.GetCurrentDirectory(), folderPath);
    }

    private static bool IsSupportedImage(string path) =>
        SupportedExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);

    private static int MapOutcome(ReviewBannerOutcome outcome, string imagePath) =>
        outcome switch
        {
            ReviewBannerOutcome.Success success => PrintSuccess(success),
            ReviewBannerOutcome.FileNotFound notFound => PrintError($"Không tìm thấy file: {notFound.Path}", 1),
            ReviewBannerOutcome.MissingApiKey => PrintError(
                "Thiếu hoặc hết hạn API key Gemini.", 1),
            ReviewBannerOutcome.InvalidResponse invalid => PrintError(
                $"LLM không hợp lệ ({Path.GetFileName(imagePath)}). Raw: {invalid.RawText}", 2),
            ReviewBannerOutcome.ApiError api => PrintError($"Lỗi ({Path.GetFileName(imagePath)}): {api.Message}", 1),
            _ => PrintError("Lỗi không xác định.", 1)
        };

    private static int PrintSuccess(ReviewBannerOutcome.Success success)
    {
        Console.WriteLine($"  → {success.Label}");
        return 0;
    }

    private static int PrintError(string message, int exitCode)
    {
        Console.Error.WriteLine($"  → {message}");
        return exitCode;
    }
}
