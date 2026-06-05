using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Domain.Models;
using IOFile = System.IO.File;

namespace AdOpsAgenReviewBanner.Infrastructure.Files;

/// <summary>Đọc ảnh từ đường dẫn local.</summary>
public sealed class LocalImageReader : IImageReader
{
    private readonly ITelegramNotifier _telegram;

    public LocalImageReader(ITelegramNotifier telegram)
    {
        _telegram = telegram;
    }

    public async Task<BannerImage?> TryReadAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedPath = ResolvePath(imagePath);
            if (!IOFile.Exists(resolvedPath))
                return null;

            var bytes = await IOFile.ReadAllBytesAsync(resolvedPath, cancellationToken);
            var mimeType = GetMimeType(resolvedPath);

            return new BannerImage(bytes, mimeType, resolvedPath);
        }
        catch (Exception ex)
        {
            await _telegram.NotifyExceptionAsync(nameof(TryReadAsync), ex, cancellationToken: cancellationToken);
            throw;
        }
    }

    private static string ResolvePath(string imagePath)
    {
        if (Path.IsPathRooted(imagePath))
            return imagePath;

        string[] searchDirs =
        [
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."))
        ];

        return searchDirs
            .Select(dir => Path.Combine(dir, imagePath))
            .FirstOrDefault(IOFile.Exists)
            ?? Path.Combine(Directory.GetCurrentDirectory(), imagePath);
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            _ => "image/png"
        };
}
