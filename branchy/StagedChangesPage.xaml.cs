using Branchy.Models;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Branchy;

/// <summary>Displays changes already present in the Git index.</summary>
public sealed partial class StagedChangesPage : Page
{
    /// <summary>Initializes the staged changes surface.</summary>
    public StagedChangesPage() => InitializeComponent();

    /// <summary>Gets the shared repository state after frame navigation.</summary>
    public MainViewModel ViewModel { get; private set; } = null!;

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = ((MainPage)e.Parameter).ViewModel;
        DataContext = ViewModel;
    }

    private async void StagedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is GitFileChange change)
        {
            await ViewModel.SelectFileAsync(change, staged: true);
        }
    }

    private void UnstageFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GitFileChange change)
        {
            ViewModel.UnstageCommand.Execute(change);
        }
    }
}
