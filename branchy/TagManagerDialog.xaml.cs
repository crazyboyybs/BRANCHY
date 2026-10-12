using Branchy.Models;
using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Hosts local tag management for the active repository.</summary>
public sealed partial class TagManagerDialog : ContentDialog
{
    private readonly ILocalizationService _localizer;

    /// <summary>Initializes tag management for the active repository.</summary>
    public TagManagerDialog(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        _localizer = localizer;
        ViewModel = new TagManagerViewModel(repositoryPath, gitService, localizer);
        InitializeComponent();
        Loaded += TagManagerDialog_Loaded;
    }

    /// <summary>Gets the dialog state used by compiled bindings.</summary>
    public TagManagerViewModel ViewModel { get; }

    private async void TagManagerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= TagManagerDialog_Loaded;
        await ViewModel.LoadAsync();
    }

    private async void CreateTagButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.CreateAsync();

    private void DeleteTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTag is not GitTag tag || !ViewModel.CanDelete)
        {
            return;
        }

        DeleteTagConfirmationText.Text = _localizer.Format(
            "Tags_DeleteConfirmation",
            tag.Name);
        DeleteConfirmationPanel.Visibility = Visibility.Visible;
    }

    private async void ConfirmDeleteTagButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteConfirmationPanel.Visibility = Visibility.Collapsed;
        await ViewModel.DeleteSelectedAsync();
    }

    private void CancelDeleteTagButton_Click(object sender, RoutedEventArgs e) =>
        DeleteConfirmationPanel.Visibility = Visibility.Collapsed;

    private void CloseTagManagerButton_Click(object sender, RoutedEventArgs e) => Hide();
}
