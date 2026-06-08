namespace AdOpsAgenReviewBanner.Configuration;

/// <summary>Cấu hình Florence-2 ONNX local (section "Florence" trong appsettings.json).</summary>
public sealed class FlorenceSettings
{
    /// <summary>Thư mục chứa / tải mô hình ONNX (FlorenceModelDownloader).</summary>
    public string ModelsPath { get; set; } = "Models";

    /// <summary>Thư mục tessdata cho Tesseract OCR bổ sung (tùy chọn).</summary>
    public string TessDataPath { get; set; } = "tessdata";

    /// <summary>Bật Tesseract OCR song song Florence OCR.</summary>
    public bool EnableTesseract { get; set; } = true;

    /// <summary>Ngôn ngữ Tesseract — cần file .traineddata tương ứng trong TessDataPath.</summary>
    public string TesseractLanguages { get; set; } = "vie+eng";
}
