using Branchy.Models;
using Branchy.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Confirms restoration or discard of one tracked working-tree path.</summary>
public sealed partial class DiscardChangesDialog : ContentDialog
{
    /// <summary>Initializes the confirmation for one change.</summary>
    public DiscardChangesDialog(GitFileChange change, ILocalizationService localizer)
    {
        Change = change;
        Path = change.Path;
        Heading = localizer.GetString(
            change.IsWorkTreeDeletion
                ? "DiscardChanges_RestoreTitle"
                : "DiscardChanges_DiscardTitle");
        Message = localizer.GetString(
            change.IsWorkTreeDeletion
                ? "DiscardChanges_RestoreMessage"
                : "DiscardChanges_DiscardMessage");
        ActionLabel = localizer.GetString(
            change.IsWorkTreeDeletion
                ? "DiscardChanges_RestoreAction"
                : "DiscardChanges_DiscardAction");
        InitializeComponent();
    }

    /// <summary>Gets the change being confirmed.</summary>
    public GitFileChange Change { get; }

    /// <summary>Gets the affected repository path.</summary>
    public string Path { get; }

    /// <summary>Gets the localized confirmation heading.</summary>
    public string Heading { get; }

    /// <summary>Gets the localized consequence description.</summary>
    public string Message { get; }

    /// <summary>Gets the localized action label.</summary>
    public string ActionLabel { get; }

    /// <summary>Gets whether the user confirmed the operation.</summary>
    public bool WasConfirmed { get; private set; }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        WasConfirmed = true;
        Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Hide();
}
