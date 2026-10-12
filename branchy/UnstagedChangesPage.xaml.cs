using Branchy.Models;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Branchy;

/// <summary>Displays changes that are not yet present in the Git index.</summary>
public sealed partial class UnstagedChangesPage : Page
{
    private MainPage? _owner;

    /// <summary>Initializes the unstaged changes surface.</summary>
    public UnstagedChangesPage() => InitializeComponent();

    /// <summary>Gets the shared repository state after frame navigation.</summary>
    public MainViewModel ViewModel { get; private set; } = null!;

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _owner = (MainPage)e.Parameter;
        ViewModel = _owner.ViewModel;
        DataContext = ViewModel;
    }

    private async void UnstagedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is GitFileChange change)
        {
            await ViewModel.SelectFileAsync(change, staged: false);
        }
    }

    private async void PartialStageFile_Click(object sender, RoutedEventArgs e)
    {
        if (_owner is not null &&
            (sender as FrameworkElement)?.DataContext is GitFileChange change)
        {
            await _owner.ShowPartialStageDialogAsync(change);
        }
    }

    private void StageFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GitFileChange change)
        {
            ViewModel.StageCommand.Execute(change);
        }
    }

    private async void DiscardFile_Click(object sender, RoutedEventArgs e)
    {
        if (_owner is not null &&
            (sender as FrameworkElement)?.DataContext is GitFileChange change)
        {
            await _owner.ShowDiscardDialogAsync(change);
        }
    }
}
