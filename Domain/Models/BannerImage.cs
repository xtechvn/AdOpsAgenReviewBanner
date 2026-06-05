namespace AdOpsAgenReviewBanner.Domain.Models;

/// <summary>Ảnh banner đã đọc từ disk — bytes + MIME cho Gemini inline_data.</summary>
public sealed record BannerImage(
    byte[] Bytes,
    string MimeType,
    string SourcePath);
