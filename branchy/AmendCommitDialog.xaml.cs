using Branchy.Models;
using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Lets the user rewrite a commit with staged changes or only a new message.</summary>
public sealed partial class AmendCommitDialog : ContentDialog
{
    /// <summary>Initializes the commit selection dialog for the active repository.</summary>
    public AmendCommitDialog(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer,
        bool messageOnly = false)
    {
        ViewModel = new AmendCommitViewModel(repositoryPath, gitService, localizer);
        IsMessageOnly = messageOnly;
        InitializeComponent();
        if (messageOnly)
        {
            Title = localizer.GetString("AmendMessageDialog_Title");
            DescriptionText.Text = localizer.GetString("AmendMessageDialog_Description");
            HistoryRewriteInfoBar.Title = localizer.GetString("AmendMessageDialog_WarningTitle");
            HistoryRewriteInfoBar.Message = localizer.GetString("AmendMessageDialog_WarningMessage");
            ApplyButton.Content = localizer.GetString("AmendMessageDialog_Action");
        }

        Loaded += AmendCommitDialog_Loaded;
    }

    /// <summary>Gets the dialog state used by compiled bindings.</summary>
    public AmendCommitViewModel ViewModel { get; }

    /// <summary>Gets whether this dialog rewrites only the commit message.</summary>
    internal bool IsMessageOnly { get; }

    /// <summary>Gets the commit selected to receive the staged changes.</summary>
    internal GitCommit? SelectedCommit => ViewModel.SelectedCommit;

    /// <summary>Gets the edited commit message.</summary>
    internal string CommitMessage => ViewModel.CommitMessage;

    /// <summary>Gets whether the user confirmed the selected commit.</summary>
    internal bool WasConfirmed { get; private set; }

    private async void AmendCommitDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= AmendCommitDialog_Loaded;
        await ViewModel.LoadAsync();
    }

    private void ApplyAmendButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanConfirm)
        {
            return;
        }

        WasConfirmed = true;
        Hide();
    }

    private void CancelAmendButton_Click(object sender, RoutedEventArgs e) => Hide();
}
