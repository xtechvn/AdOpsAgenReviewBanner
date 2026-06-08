namespace AdOpsAgenReviewBanner.Application.Abstractions;

/// <summary>Trích xuất text từ ảnh bằng OCR ngoài (Tesseract).</summary>
public interface IOcrTextExtractor
{
    string ExtractText(string imagePath);
}
