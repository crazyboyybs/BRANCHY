using Branchy.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Branchy;

/// <summary>Edits a working-tree file while leaving the staged patch unchanged.</summary>
public sealed partial class QuickEditDialog : ContentDialog
{
    private readonly string _repositoryPath;
    private readonly string _relativePath;
    private readonly WorkingTreeFileService _files;
    private readonly ILocalizationService _localizer;
    private readonly WorkingTreeDocument _document;
    private bool _allowClose;
    private bool _saving;
    private bool _ready;

    /// <summary>Initializes a bounded editing session for a validated text file.</summary>
    public QuickEditDialog(string repositoryPath, string relativePath, WorkingTreeDocument document, ILocalizationService localizer, XamlRoot root)
    {
        _repositoryPath = repositoryPath;
        _relativePath = relativePath;
        _document = document;
        _localizer = localizer;
        _files = new WorkingTreeFileService(localizer);
        InitializeComponent();
        EditorLayout.Width = Math.Max(320, Math.Min(1000, root.Size.Width - 120));
        EditorLayout.Height = Math.Max(240, Math.Min(650, root.Size.Height - 180));
        FilePathText.Text = relativePath;
        Editor.Text = document.Text;
        _ready = true;
    }

    /// <summary>Gets whether the file was saved.</summary>
    public bool WasSaved { get; private set; }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) SaveButton.IsEnabled = !_saving && Editor.Text != _document.Text;
    }

    private async Task SaveAsync()
    {
        if (_saving || !SaveButton.IsEnabled) return;
        _saving = true;
        SaveButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        Editor.IsReadOnly = true;
        ErrorInfo.IsOpen = false;
        try
        {
            await _files.SaveAsync(_repositoryPath, _relativePath, _document, Editor.Text);
            WasSaved = true;
            _allowClose = true;
            Hide();
        }
        catch (Exception exception)
        {
            ErrorInfo.Message = exception.Message;
            ErrorInfo.IsOpen = true;
        }
        finally
        {
            _saving = false;
            Editor.IsReadOnly = false;
            CancelButton.IsEnabled = true;
            SaveButton.IsEnabled = Editor.Text != _document.Text;
        }
    }

    private void OpenSearch()
    {
        SearchBox.Visibility = Visibility.Visible;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void Find(bool next)
    {
        if (string.IsNullOrEmpty(SearchBox.Text)) return;
        int start = next ? Math.Min(Editor.Text.Length, Editor.SelectionStart + Math.Max(1, Editor.SelectionLength)) : 0;
        int index = Editor.Text.IndexOf(SearchBox.Text, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0 && start > 0) index = Editor.Text.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            ErrorInfo.Message = _localizer.GetString("QuickEdit_NoMatch");
            ErrorInfo.IsOpen = true;
            return;
        }
        ErrorInfo.IsOpen = false;
        Editor.Select(index, SearchBox.Text.Length);
        Editor.Focus(FocusState.Programmatic);
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (_saving || (!_allowClose && Editor.Text != _document.Text))
        {
            args.Cancel = true;
            ErrorInfo.Message = _localizer.GetString("QuickEdit_Unsaved");
            ErrorInfo.IsOpen = true;
        }
    }
    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) { _allowClose = true; Hide(); }
    private void Search_Submitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => Find(true);
    private void Find_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { OpenSearch(); args.Handled = true; }
    private void Next_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { Find(true); args.Handled = true; }
    private async void Save_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { args.Handled = true; await SaveAsync(); }
}
