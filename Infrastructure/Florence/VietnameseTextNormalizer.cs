using System.Globalization;
using System.Text;

namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Chuẩn hóa text để so khớp keyword (OCR thường mất dấu tiếng Việt).</summary>
public static class VietnameseTextNormalizer
{
    public static string NormalizeForMatch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lower = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
