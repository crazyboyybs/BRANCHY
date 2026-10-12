using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Hosts remote management for the active repository.</summary>
public sealed partial class RemoteManagerDialog : ContentDialog
{
    /// <summary>Initializes the remote manager.</summary>
    public RemoteManagerDialog(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        ViewModel = new RemoteManagerViewModel(repositoryPath, gitService, localizer);
        InitializeComponent();
        Loaded += RemoteManagerDialog_Loaded;
    }

    /// <summary>Gets the remote manager state used by compiled bindings.</summary>
    public RemoteManagerViewModel ViewModel { get; }

    private async void RemoteManagerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= RemoteManagerDialog_Loaded;
        await ViewModel.LoadAsync();
    }

    private void AddRemoteButton_Click(object sender, RoutedEventArgs e) => ViewModel.StartCreate();

    private async void SaveRemoteButton_Click(object sender, RoutedEventArgs e) => await ViewModel.SaveAsync();

    private async void FetchRemoteButton_Click(object sender, RoutedEventArgs e) => await ViewModel.FetchSelectedAsync();

    private void DeleteRemoteButton_Click(object sender, RoutedEventArgs e) => ViewModel.RequestDelete();

    private async void ConfirmDeleteRemoteButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ConfirmDeleteAsync();

    private void CancelDeleteRemoteButton_Click(object sender, RoutedEventArgs e) => ViewModel.CancelDelete();

    private void CloseRemoteManagerButton_Click(object sender, RoutedEventArgs e) => Hide();
}
