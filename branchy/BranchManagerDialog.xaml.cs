using Branchy.Models;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Hosts branch operations without coupling them to the main workspace layout.</summary>
public sealed partial class BranchManagerDialog : ContentDialog
{
    /// <summary>Initializes the dialog with the active repository ViewModel.</summary>
    public BranchManagerDialog(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>Gets the shared repository ViewModel.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>Gets the branch requested for merge after this dialog closes.</summary>
    internal GitBranch? RequestedMergeBranch { get; private set; }

    private void MergeBranchButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanMergeSelectedBranch || ViewModel.SelectedBranch is not GitBranch branch)
        {
            return;
        }

        RequestedMergeBranch = branch;
        Hide();
    }

    private void CloseBranchDialogButton_Click(object sender, RoutedEventArgs e) => Hide();
}
