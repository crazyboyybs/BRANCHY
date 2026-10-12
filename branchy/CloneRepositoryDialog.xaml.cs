using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Collects a repository source and a new local destination.</summary>
public sealed partial class CloneRepositoryDialog : ContentDialog
{
    /// <summary>Initializes the clone dialog.</summary>
    public CloneRepositoryDialog(
        IGitService gitService,
        IRepositoryPickerService repositoryPicker,
        ILocalizationService localizer,
        string initialDestinationParent)
    {
        ViewModel = new CloneRepositoryViewModel(gitService, repositoryPicker, localizer)
        {
            DestinationParent = initialDestinationParent,
        };
        InitializeComponent();
        Loaded += CloneRepositoryDialog_Loaded;
    }

    /// <summary>Gets the clone workflow used by compiled bindings.</summary>
    public CloneRepositoryViewModel ViewModel { get; }

    /// <summary>Gets whether the repository was cloned successfully.</summary>
    internal bool WasCloned { get; private set; }

    /// <summary>Gets the cloned working directory.</summary>
    internal string? ClonedRepositoryPath => ViewModel.ClonedRepositoryPath;

    private void CloneRepositoryDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CloneRepositoryDialog_Loaded;
        RepositorySourceTextBox.Focus(FocusState.Programmatic);
    }

    private async void BrowseCloneDestinationButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.BrowseDestinationAsync();

    private async void CloneRepositoryConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.CloneAsync())
        {
            WasCloned = true;
            Hide();
        }
    }

    private void CancelCloneRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanCancel)
        {
            Hide();
        }
    }
}
