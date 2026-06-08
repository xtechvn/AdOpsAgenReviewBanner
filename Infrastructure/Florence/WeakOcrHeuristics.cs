namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Phát hiện caption/OCR quá nghèo — nên NeedsReview thay vì Allowed.</summary>
internal static class WeakOcrHeuristics
{
    private static readonly string[] GenericCaptionMarkers =
    [
        "the image is a screen",
        "letters are in a neon",
        "the background of the screen is black",
        "the text is white"
    ];

    public static bool IsGenericAiCaption(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return true;

        var lower = caption.ToLowerInvariant();
        return GenericCaptionMarkers.Count(marker => lower.Contains(marker, StringComparison.Ordinal)) >= 2;
    }

    public static bool HasMeaningfulReadableText(string combinedText)
    {
        if (string.IsNullOrWhiteSpace(combinedText))
            return false;

        var letters = combinedText.Count(char.IsLetter);
        return letters >= 12;
    }
}
