using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Displays selectable hunks for one tracked working-tree change.</summary>
public sealed partial class PartialStageDialog : ContentDialog
{
    /// <summary>Initializes partial staging for one path.</summary>
    public PartialStageDialog(
        string repositoryPath,
        string path,
        IGitService gitService,
        ILocalizationService localizer)
    {
        ViewModel = new PartialStageViewModel(
            repositoryPath,
            path,
            gitService,
            localizer);
        InitializeComponent();
        Loaded += PartialStageDialog_Loaded;
    }

    /// <summary>Gets the partial-stage state used by compiled bindings.</summary>
    public PartialStageViewModel ViewModel { get; }

    /// <summary>Gets whether selected hunks were applied to the index.</summary>
    public bool WasApplied => ViewModel.WasApplied;

    private async void PartialStageDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PartialStageDialog_Loaded;
        await ViewModel.LoadAsync();
    }

    private void SelectAllHunksButton_Click(object sender, RoutedEventArgs e) => ViewModel.SelectAll();

    private void ClearHunkSelectionButton_Click(object sender, RoutedEventArgs e) => ViewModel.ClearSelection();

    private async void ApplySelectedHunksButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyAsync();
        if (ViewModel.WasApplied)
        {
            Hide();
        }
    }

    private void ClosePartialStageButton_Click(object sender, RoutedEventArgs e) => Hide();
}
