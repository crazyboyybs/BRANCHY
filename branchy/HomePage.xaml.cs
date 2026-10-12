using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Branchy;

/// <summary>Retains the repository workspace surface across native frame navigation.</summary>
public sealed partial class HomePage : Page
{
    /// <summary>Initializes the cached home page.</summary>
    public HomePage() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // MainPage owns bindings and commands; the cached page keeps their visual state intact.
        if (Content is null && e.Parameter is FrameworkElement surface) Content = surface;
    }
}
