using Branchy.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Branchy;

/// <summary>Displays every path changed by the selected history commit.</summary>
public sealed partial class HistoryFilesPage : Page
{
    /// <summary>Initializes the history files surface.</summary>
    public HistoryFilesPage() => InitializeComponent();

    /// <summary>Gets the shared history state after frame navigation.</summary>
    public HistoryViewModel ViewModel { get; private set; } = null!;

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = (HistoryViewModel)e.Parameter;
        DataContext = ViewModel;
    }
}
