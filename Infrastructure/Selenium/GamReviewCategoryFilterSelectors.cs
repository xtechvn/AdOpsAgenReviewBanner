namespace AdOpsAgenReviewBanner.Infrastructure.Selenium;

/// <summary>XPath filter General ad category — Ad review center (Reviewed worker).</summary>
internal static class GamReviewCategoryFilterSelectors
{
    public static string CategoryDialog(string menuLabel) =>
        $"//div[@role='dialog'][@aria-label='{menuLabel}']";

    public static string CategoryApplyButton(string menuLabel) =>
        $"{CategoryDialog(menuLabel)}//material-button[@aria-label='Apply']";

    public static string CategoryTreeItems(string menuLabel) =>
        $"{CategoryDialog(menuLabel)}//div[@role='treeitem']";

    public static string CategoryTreeItemCheckbox(string menuLabel, int oneBasedIndex) =>
        $"({CategoryTreeItems(menuLabel)})[{oneBasedIndex}]//material-checkbox";

    public static string[] CategoryMenuItemCandidates(string menuLabel) =>
    [
        $"//material-popup[contains(@class,'filter-suggestion-popup')]//material-select-item[@aria-label='{menuLabel}']",
        $"//material-list[contains(@class,'suggestion-list')]//material-select-item[@aria-label='{menuLabel}']",
        $"//material-select-item[@aria-label='{menuLabel}']"
    ];
}
