namespace AdOpsAgenReviewBanner.Infrastructure.Florence;

/// <summary>Từ khóa mặc định EN + VI (fallback khi API chưa bật).</summary>
internal static class BuiltinKeywordDefaults
{
    public static readonly string[] BlockPhrases =
    [
        "real money", "slot machine", "sign-up bonus", "sign up bonus",
        "fake money", "victoria secret", "victorias secret", "victoria's secret",
        "mobile game", "online casino", "sports betting", "shop on temu",
        "cược thể thao", "cuoc the thao", "cược ngay", "cuoc ngay",
        "đặt cược", "dat cuoc", "cá cược", "ca cuoc", "cá độ", "ca do",
        "ca cuoc the thao", "nạp tiền", "nap tien", "khuyến mãi", "khuyen mai"
    ];

    public static readonly string[] BlockStems =
    [
        "gambl", "casino", "poker", "roulette", "blackjack", "jackpot",
        "lottery", "wager", "betting", "slots"
    ];

    public static readonly string[] BlockWords =
    [
        "temu", "sexy", "nude", "naked", "porn", "adult", "erotic", "nsfw", "bikini", "lingerie",
        "underwear", "topless", "provocative",
        "violence", "violent", "blood", "gore", "weapon", "gun", "knife", "murder",
        "scam", "counterfeit", "hack", "cheat", "phishing",
        "marijuana", "cannabis", "cocaine",
        "777", "chip",
        "cược", "cuoc", "thưởng", "thuong", "lô đề", "lo de", "xổ số", "xo so", "cá độ", "ca do"
    ];

    public static readonly string[] ReviewPhrases =
    [
        "weight loss", "diet pill", "sign up", "real cash"
    ];

    public static readonly string[] ReviewStems =
    [
        "alcohol", "crypto", "bitcoin", "forex", "dating", "beauty", "cosmetic", "supplement"
    ];

    public static readonly string[] ReviewWords =
    [
        "beer", "wine", "vodka", "whiskey", "liquor", "drunk",
        "hookup", "tinder", "singles", "trading", "surgery", "secret",
        "loan", "credit", "debt", "game", "gaming"
    ];
}
