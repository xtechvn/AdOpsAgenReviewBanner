namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>XPath GAM Ad review center — lọc Creative ID và Allow/Block hàng loạt.</summary>
internal static class GamBlockedReviewSelectors
{
    public const string FilterSearchInput =
        "//filter-bar//input[@role='combobox' and contains(@aria-label,'Filter or search')]";

    public static readonly string[] CreativeIdMenuItemCandidates =
    [
        "//material-popup[contains(@class,'filter-suggestion-popup')]//material-select-item[@aria-label='Creative ID']",
        "//material-list[contains(@class,'suggestion-list')]//material-select-item[@aria-label='Creative ID']",
        "//material-select-item[@aria-label='Creative ID']//span[contains(@class,'menu-item-label') and normalize-space()='Creative ID']/ancestor::material-select-item[1]"
    ];

    public static readonly string[] CreativeIdValueInputCandidates =
    [
        "//div[@role='dialog'][@aria-label='Creative ID']//material-input//input[@type='text']",
        "//filter-editor-string//input[@type='text']",
        "//div[contains(@class,'editor-container')]//input[@type='text']"
    ];

    public const string CreativeIdApplyButton =
        "//div[@role='dialog'][@aria-label='Creative ID']//material-button[@aria-label='Apply']";

    public const string SelectAllAdsButton =
        "//multiple-selection-bar//button[contains(@class,'select-all')][@aria-label='Select all ads']";

    public const string AllowButton =
        "//multiple-selection-bar//button[contains(@class,'allow-button')]";

    public const string BlockButton =
        "//multiple-selection-bar//button[contains(@class,'block-button')]";

    public static readonly string[] GridAdCardCandidates =
    [
        "//div[contains(@class,'reviewable') and @role='row']",
        "//creative-preview[@role='img']"
    ];
}
