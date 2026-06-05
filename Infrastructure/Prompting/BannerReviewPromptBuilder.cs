using AdOpsAgenReviewBanner.Application.Abstractions;
using AdOpsAgenReviewBanner.Domain.Models;
using System.Text;

namespace AdOpsAgenReviewBanner.Infrastructure.Prompting;

/// <summary>Prompt kiểm duyệt Ad Ops — ưu tiên chặn nội dung nhạy cảm (lingerie, bikini, gợi cảm).</summary>
public sealed class BannerReviewPromptBuilder : IPromptBuilder
{
    private readonly ITelegramNotifier _telegram;

    public BannerReviewPromptBuilder(ITelegramNotifier telegram)
    {
        _telegram = telegram;
    }

    public string Build(ReviewPolicy policy)
    {
        try
        {
            var blocked = policy.BlockedLabel;
            var reviewed = policy.ReviewedLabel;

            var sb = new StringBuilder();
            sb.AppendLine("You are a strict Ad Ops banner moderator. Review the attached ad image.");
            sb.AppendLine($"Reply with EXACTLY one word: {blocked} or {reviewed}.");
            sb.AppendLine("No explanation. No extra words. No punctuation.");
            sb.AppendLine();
            sb.AppendLine("DECISION RULES (follow strictly):");
            sb.AppendLine($"- Default to {blocked} when the image shows revealing clothing on a person or sexualized body focus.");
            sb.AppendLine("- Brand reputation does NOT make an ad safe (e.g. lingerie/beauty retailers still → Blocked if imagery is revealing).");
            sb.AppendLine("- Block if you see ANY of: bikini, swimwear, bra, lingerie, underwear, lace/intimate apparel on a model, exposed midriff/navel with revealing top, cleavage focus, suggestive pose, erotic or provocative tone.");
            sb.AppendLine("- Block fragrance/beauty/fashion ads when the visual uses lingerie-like or minimal clothing imagery (not only explicit nudity).");
            sb.AppendLine($"- Reply {reviewed} ONLY when people (if any) are fully non-revealing clothed and the ad has no sexualized body focus (e.g. education, finance, food, neutral product packshot).");
            sb.AppendLine($"- If unsure between {blocked} and {reviewed} for body/sexual tone → reply {blocked}.");
            sb.AppendLine();
            sb.AppendLine("Blocked policy categories:");

            foreach (var category in policy.Categories)
            {
                sb.Append("- ");
                sb.AppendLine(category.Description.Trim());

                if (category.Keywords.Count > 0)
                {
                    sb.Append("  Keywords: ");
                    sb.AppendLine(string.Join(", ", category.Keywords));
                }
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            _ = _telegram.NotifyExceptionAsync(nameof(Build), ex);
            throw;
        }
    }
}
