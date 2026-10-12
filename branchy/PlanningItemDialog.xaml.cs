using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Branchy;

/// <summary>Creates or edits a planning item and its optional commit association.</summary>
public sealed partial class PlanningItemDialog : ContentDialog
{
    private readonly ILocalizationService _localizer;
    private readonly PlanningItem? _existingItem;

    /// <summary>Initializes the item editor.</summary>
    public PlanningItemDialog(
        ILocalizationService localizer,
        IReadOnlyList<GitHistoryEntry> commits,
        PlanningItem? existingItem = null)
    {
        _localizer = localizer;
        _existingItem = existingItem;
        CommitOptions.Add(new PlanningCommitOption(
            null,
            localizer.GetString("Planning_CommitNone"),
            string.Empty));
        foreach (GitHistoryEntry commit in commits)
        {
            CommitOptions.Add(new PlanningCommitOption(
                commit,
                $"{commit.ShortHash} · {commit.Subject}",
                commit.AuthorAndDate));
        }

        InitializeComponent();
        Title = localizer.GetString(existingItem is null
            ? "Planning_ItemDialogCreateTitle"
            : "Planning_ItemDialogEditTitle");
        SaveButton.Content = localizer.GetString(existingItem is null
            ? "Planning_CreateAction"
            : "Planning_SaveAction");
        CancelButton.Content = localizer.GetString("Common_Cancel");

        TypeComboBox.ItemsSource = CreateTypeOptions(localizer);
        StatusComboBox.ItemsSource = CreateStatusOptions(localizer);
        PopulateFields();
    }

    /// <summary>Gets the commits available for association.</summary>
    public ObservableCollection<PlanningCommitOption> CommitOptions { get; } = [];

    /// <summary>Gets whether the primary action was confirmed.</summary>
    public bool WasConfirmed { get; private set; }

    /// <summary>Gets the item produced by the editor.</summary>
    public PlanningItem? ResultItem { get; private set; }

    private void PopulateFields()
    {
        PlanningItem item = _existingItem ?? new PlanningItem();
        TitleTextBox.Text = item.Title;
        DescriptionTextBox.Text = item.Description;
        DocumentationTextBox.Text = item.Documentation;
        TagsTextBox.Text = string.Join(", ", item.Tags);
        ProgressSlider.Value = item.Progress;
        TypeComboBox.SelectedItem = ((IEnumerable<PlanningChoice<PlanningItemType>>)TypeComboBox.ItemsSource)
            .First(option => option.Value == item.Type);
        StatusComboBox.SelectedItem = ((IEnumerable<PlanningChoice<PlanningItemStatus>>)StatusComboBox.ItemsSource)
            .First(option => option.Value == item.Status);
        CommitComboBox.SelectedItem = CommitOptions.FirstOrDefault(option =>
            string.Equals(option.Commit?.Hash, item.LinkedCommitHash, StringComparison.OrdinalIgnoreCase)) ??
            CommitOptions[0];
        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(item.Title);
        UpdateProgressText();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleTextBox.Text.Trim();
        if (title.Length == 0)
        {
            ValidationInfoBar.Message = _localizer.GetString("Planning_TitleRequired");
            ValidationInfoBar.IsOpen = true;
            TitleTextBox.Focus(FocusState.Programmatic);
            return;
        }

        var selectedType = (PlanningChoice<PlanningItemType>)TypeComboBox.SelectedItem;
        var selectedStatus = (PlanningChoice<PlanningItemStatus>)StatusComboBox.SelectedItem;
        var selectedCommit = (PlanningCommitOption)CommitComboBox.SelectedItem;
        GitHistoryEntry? commit = selectedCommit.Commit;
        ResultItem = new PlanningItem
        {
            Id = _existingItem?.Id ?? Guid.NewGuid(),
            Title = title,
            Description = DescriptionTextBox.Text.Trim(),
            Documentation = DocumentationTextBox.Text.Trim(),
            Type = selectedType.Value,
            Status = selectedStatus.Value,
            Progress = (int)Math.Round(ProgressSlider.Value),
            Tags = TagsTextBox.Text
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            LinkedCommitHash = commit?.Hash ?? string.Empty,
            LinkedCommitShortHash = commit?.ShortHash ?? string.Empty,
            LinkedCommitSubject = commit?.Subject ?? string.Empty,
            CreatedAt = _existingItem?.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        WasConfirmed = true;
        Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(TitleTextBox.Text);
        SaveButton.IsEnabled = hasTitle;
        if (hasTitle)
        {
            ValidationInfoBar.IsOpen = false;
        }
    }

    private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        UpdateProgressText();

    private void UpdateProgressText()
    {
        if (ProgressValueText is not null)
        {
            ProgressValueText.Text = _localizer.Format(
                "Planning_ProgressValue",
                (int)Math.Round(ProgressSlider.Value));
        }
    }

    private static IReadOnlyList<PlanningChoice<PlanningItemType>> CreateTypeOptions(
        ILocalizationService localizer) =>
        Enum.GetValues<PlanningItemType>()
            .Select(value => new PlanningChoice<PlanningItemType>(
                value,
                localizer.GetString($"Planning_Type_{value}")))
            .ToArray();

    private static IReadOnlyList<PlanningChoice<PlanningItemStatus>> CreateStatusOptions(
        ILocalizationService localizer) =>
        Enum.GetValues<PlanningItemStatus>()
            .Select(value => new PlanningChoice<PlanningItemStatus>(
                value,
                localizer.GetString($"Planning_Status_{value}")))
            .ToArray();
}

/// <summary>Presents a localized enum choice in a planning editor.</summary>
public sealed record PlanningChoice<T>(T Value, string Label) where T : struct, Enum;

/// <summary>Presents an optional history commit in the planning editor.</summary>
public sealed class PlanningCommitOption
{
    /// <summary>Initializes a commit option for XAML activation.</summary>
    public PlanningCommitOption()
    {
    }

    /// <summary>Initializes a commit option.</summary>
    public PlanningCommitOption(
        GitHistoryEntry? commit,
        string primaryText,
        string secondaryText)
    {
        Commit = commit;
        PrimaryText = primaryText;
        SecondaryText = secondaryText;
    }

    /// <summary>Gets or sets the associated history entry.</summary>
    public GitHistoryEntry? Commit { get; set; }

    /// <summary>Gets or sets the primary display text.</summary>
    public string PrimaryText { get; set; } = string.Empty;

    /// <summary>Gets or sets the secondary display text.</summary>
    public string SecondaryText { get; set; } = string.Empty;

    /// <summary>Gets whether secondary commit metadata should be displayed.</summary>
    public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);
}
