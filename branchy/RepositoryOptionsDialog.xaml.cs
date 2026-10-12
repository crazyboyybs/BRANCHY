using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Hosts repository-specific file inclusion options.</summary>
public sealed partial class RepositoryOptionsDialog : ContentDialog
{
    /// <summary>Initializes repository options for the active repository.</summary>
    public RepositoryOptionsDialog(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        ViewModel = new RepositoryOptionsViewModel(
            repositoryPath,
            gitService,
            localizer);
        InitializeComponent();
        Loaded += RepositoryOptionsDialog_Loaded;
    }

    /// <summary>Gets the repository options state used by compiled bindings.</summary>
    public RepositoryOptionsViewModel ViewModel { get; }

    private async void RepositoryOptionsDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= RepositoryOptionsDialog_Loaded;
        await ViewModel.LoadAsync();
    }

    private void SelectAllRepositoryFilesButton_Click(object sender, RoutedEventArgs e) => ViewModel.SelectAll();

    private void ClearRepositoryFilesButton_Click(object sender, RoutedEventArgs e) => ViewModel.ClearOptionalSelection();

    private async void SaveGitIgnoreButton_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveGitIgnoreAsync();

    private void CloseRepositoryOptionsButton_Click(object sender, RoutedEventArgs e) => Hide();
}
