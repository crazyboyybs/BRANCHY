using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Branchy;

/// <summary>Shows a rounded confirmation surface for a repository operation.</summary>
public sealed partial class OperationConfirmationDialog : ContentDialog
{
    /// <summary>Initializes the localized operation confirmation.</summary>
    public OperationConfirmationDialog(
        string dialogTitle,
        string heading,
        string message,
        string detail,
        string actionLabel)
    {
        DialogTitle = dialogTitle;
        Heading = heading;
        Message = message;
        Detail = detail;
        ActionLabel = actionLabel;
        InitializeComponent();
    }

    /// <summary>Gets the dialog title.</summary>
    public string DialogTitle { get; }

    /// <summary>Gets the operation heading.</summary>
    public string Heading { get; }

    /// <summary>Gets the operation explanation.</summary>
    public string Message { get; }

    /// <summary>Gets the branch or commit identifying the target.</summary>
    public string Detail { get; }

    /// <summary>Gets the confirmation button label.</summary>
    public string ActionLabel { get; }

    /// <summary>Gets whether the operation was confirmed.</summary>
    internal bool WasConfirmed { get; private set; }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        WasConfirmed = true;
        Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Hide();
}
