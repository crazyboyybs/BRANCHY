using Branchy.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Branchy;

/// <summary>Displays the textual patch introduced by the selected history commit.</summary>
public sealed partial class HistoryPatchPage : Page
{
    /// <summary>Initializes the history patch surface.</summary>
    public HistoryPatchPage() => InitializeComponent();

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
