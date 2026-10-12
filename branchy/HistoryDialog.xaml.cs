using Branchy.Models;
using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Branchy;

/// <summary>Displays repository commits, metadata, changed paths, and textual patches.</summary>
public sealed partial class HistoryDialog : ContentDialog
{
    private const double CollapsedMessageHeight = 42;
    private const double ExpandedMessageHeight = 210;
    private readonly bool _allowRevert;
    private CancellationTokenSource? _searchCancellation;
    private bool _isMessageExpanded;
    private bool _isAnimatingMessage;
    private bool _isHistoryLoaded;

    /// <summary>Initializes the visual history for the active repository.</summary>
    public HistoryDialog(
        string repositoryPath,
        IGitService gitService,
        IArchivePickerService archivePicker,
        ILocalizationService localizer,
        bool allowRevert)
    {
        _allowRevert = allowRevert;
        ViewModel = new HistoryViewModel(repositoryPath, gitService, archivePicker, localizer);
        InitializeComponent();
        Loaded += HistoryDialog_Loaded;
        Unloaded += HistoryDialog_Unloaded;
    }

    /// <summary>Gets the history state used by compiled bindings.</summary>
    public HistoryViewModel ViewModel { get; }

    /// <summary>Gets the commit requested for revert after this dialog closes.</summary>
    internal GitHistoryEntry? RequestedRevertCommit { get; private set; }

    private async void HistoryDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HistoryDialog_Loaded;
        HistoryDetailSelector.SelectedItem = PatchSelectorItem;
        await ViewModel.LoadAsync();
        _isHistoryLoaded = true;
        CommitList.SelectedItem = ViewModel.SelectedCommit;
        UpdateRevertAvailability();
    }

    private void HistoryDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= HistoryDialog_Unloaded;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
        ViewModel.Dispose();
    }

    private async void CommitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is GitHistoryEntry commit)
        {
            await ViewModel.SelectCommitAsync(commit);
            UpdateRevertAvailability();
        }
    }

    private async void HistorySearchBox_TextChanged(
        AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_isHistoryLoaded && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            await RunHistorySearchAsync(sender.Text, debounce: true);
        }
    }

    private async void HistorySearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_isHistoryLoaded)
        {
            await RunHistorySearchAsync(args.QueryText, debounce: false);
        }
    }

    private async void HistorySearchScopeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isHistoryLoaded)
        {
            await RunHistorySearchAsync(HistorySearchBox.Text, debounce: false);
        }
    }

    private async Task RunHistorySearchAsync(string query, bool debounce)
    {
        var currentSearch = new CancellationTokenSource();
        CancellationTokenSource? previousSearch = _searchCancellation;
        _searchCancellation = currentSearch;
        previousSearch?.Cancel();
        CancellationToken token = currentSearch.Token;
        try
        {
            if (debounce)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), token);
            }

            int selectedIndex = Math.Clamp(HistorySearchScopeComboBox.SelectedIndex, 0, 3);
            await ViewModel.SearchAsync(query, (HistorySearchScope)selectedIndex, token);
            token.ThrowIfCancellationRequested();
            CommitList.SelectedItem = ViewModel.SelectedCommit;
            UpdateRevertAvailability();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            currentSearch.Dispose();
            if (ReferenceEquals(_searchCancellation, currentSearch))
            {
                _searchCancellation = null;
            }
        }
    }

    private async void ExportHistoryCommitButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ExportSelectedCommitAsync();

    private void RevertHistoryCommitButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_allowRevert || ViewModel.SelectedCommit is not GitHistoryEntry commit || ViewModel.IsBusy)
        {
            return;
        }

        RequestedRevertCommit = commit;
        Hide();
    }

    private void UpdateRevertAvailability() =>
        RevertHistoryCommitButton.IsEnabled =
            _allowRevert && ViewModel.SelectedCommit is not null && !ViewModel.IsBusy;

    private void HistoryDetailSelector_SelectionChanged(
        SelectorBar sender,
        SelectorBarSelectionChangedEventArgs args)
    {
        bool showFiles = ReferenceEquals(sender.SelectedItem, FilesSelectorItem);
        Type pageType = showFiles ? typeof(HistoryFilesPage) : typeof(HistoryPatchPage);
        if (HistoryDetailFrame.SourcePageType == pageType)
        {
            return;
        }

        var transition = new SlideNavigationTransitionInfo
        {
            Effect = showFiles
                ? SlideNavigationTransitionEffect.FromRight
                : SlideNavigationTransitionEffect.FromLeft,
        };
        HistoryDetailFrame.Navigate(pageType, ViewModel, transition);
    }

    private async void ToggleMessageExpansionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isAnimatingMessage)
        {
            return;
        }

        bool expanding = !_isMessageExpanded;
        _isAnimatingMessage = true;
        if (expanding)
        {
            ExpandMessageButton.Visibility = Visibility.Collapsed;
            CollapseMessageButton.Visibility = Visibility.Visible;
        }

        await AnimateCommitMessageHeightAsync(expanding);
        _isMessageExpanded = expanding;
        if (!expanding)
        {
            CommitMessageScroller.ChangeView(null, 0, null, true);
            CollapseMessageButton.Visibility = Visibility.Collapsed;
            ExpandMessageButton.Visibility = Visibility.Visible;
        }

        _isAnimatingMessage = false;
    }

    private Task AnimateCommitMessageHeightAsync(bool expanding)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation
        {
            From = CommitMessageSurface.ActualHeight,
            To = expanding ? ExpandedMessageHeight : CollapsedMessageHeight,
            Duration = TimeSpan.FromMilliseconds(expanding ? 220 : 180),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase
            {
                EasingMode = expanding ? EasingMode.EaseOut : EasingMode.EaseInOut,
            },
        };
        Storyboard.SetTarget(animation, CommitMessageSurface);
        Storyboard.SetTargetProperty(animation, "Height");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => completion.TrySetResult();
        storyboard.Begin();
        return completion.Task;
    }

    private void CloseHistoryDialogButton_Click(object sender, RoutedEventArgs e) => Hide();
}
