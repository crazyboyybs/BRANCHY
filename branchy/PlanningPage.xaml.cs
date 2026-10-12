using Branchy.Models;
using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace Branchy;

/// <summary>Displays the colorful repository planning board.</summary>
public sealed partial class PlanningPage : Page
{
    private const double BoardMinimumWidth = 1264;
    private readonly ILocalizationService _localizer;
    private readonly IPlanningBoardService _boardService;
    private readonly IGitService _gitService;
    private IReadOnlyList<GitHistoryEntry> _commits = [];
    private CancellationTokenSource? _loadCancellation;
    private Guid? _draggedItemId;
    private string _boardRepositoryPath = string.Empty;
    private Task _boardLoadTask = Task.CompletedTask;

    /// <summary>Initializes the planning page and its ViewModel.</summary>
    public PlanningPage()
    {
        _localizer = new LocalizationService();
        _boardService = new PlanningBoardService();
        _gitService = new GitService(_localizer);
        ViewModel = new PlanningBoardViewModel(_boardService, _localizer);
        InitializeComponent();
        Loaded += PlanningPage_Loaded;
    }

    /// <summary>Gets the ViewModel used by compiled XAML bindings.</summary>
    public PlanningBoardViewModel ViewModel { get; }

    /// <inheritdoc />
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not PlanningPageContext context)
        {
            return;
        }

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _loadCancellation.Token;
        Task boardTask = EnsureBoardLoadedAsync(context);
        Task historyTask = LoadHistoryAsync(context.RepositoryPath, cancellationToken);
        await Task.WhenAll(boardTask, historyTask);
    }

    private Task EnsureBoardLoadedAsync(PlanningPageContext context)
    {
        if (string.Equals(_boardRepositoryPath, context.RepositoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return _boardLoadTask;
        }

        _boardRepositoryPath = context.RepositoryPath;
        _boardLoadTask = LoadBoardAfterAsync(_boardLoadTask, context);
        return _boardLoadTask;
    }

    private async Task LoadBoardAfterAsync(Task previousLoad, PlanningPageContext context)
    {
        // Serialize repository changes; returning to a cached board preserves filters and scroll position.
        await previousLoad;
        await ViewModel.LoadAsync(context.RepositoryPath, context.RepositoryName);
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        base.OnNavigatedFrom(e);
    }

    private async Task LoadHistoryAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        try
        {
            _commits = await _gitService.GetHistoryAsync(repositoryPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // The board remains usable when history cannot be loaded.
            _commits = [];
        }
    }

    private void PlanningPage_Loaded(object sender, RoutedEventArgs e) =>
        QueueBoardSizeUpdate();

    private void BoardScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueBoardSizeUpdate();

    private void QueueBoardSizeUpdate() => DispatcherQueue.TryEnqueue(() =>
    {
        double viewportWidth = BoardScrollViewer.ViewportWidth > 0
            ? BoardScrollViewer.ViewportWidth
            : BoardScrollViewer.ActualWidth;
        double viewportHeight = BoardScrollViewer.ViewportHeight > 0
            ? BoardScrollViewer.ViewportHeight
            : BoardScrollViewer.ActualHeight;
        double targetWidth = Math.Max(BoardMinimumWidth, viewportWidth);

        if (Math.Abs(BoardGrid.Width - targetWidth) > 0.5 || double.IsNaN(BoardGrid.Width))
        {
            BoardGrid.Width = targetWidth;
        }

        if (viewportHeight > 0 &&
            (Math.Abs(BoardGrid.Height - viewportHeight) > 0.5 || double.IsNaN(BoardGrid.Height)))
        {
            BoardGrid.Height = viewportHeight;
        }
    });

    private async void NewItemButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PlanningItemDialog(_localizer, _commits);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasConfirmed && dialog.ResultItem is PlanningItem item)
        {
            await ViewModel.AddAsync(item);
        }
    }

    private async void EditCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid itemId } ||
            ViewModel.CreateEditableCopy(itemId) is not PlanningItem item)
        {
            return;
        }

        var dialog = new PlanningItemDialog(_localizer, _commits, item);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasConfirmed && dialog.ResultItem is PlanningItem updatedItem)
        {
            await ViewModel.UpdateAsync(updatedItem);
        }
    }

    private async void DeleteCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid itemId } ||
            ViewModel.CreateEditableCopy(itemId) is not PlanningItem item)
        {
            return;
        }

        var dialog = new OperationConfirmationDialog(
            _localizer.GetString("Planning_DeleteDialogTitle"),
            _localizer.Format("Planning_DeleteHeading", item.Title),
            _localizer.GetString("Planning_DeleteMessage"),
            item.Title,
            _localizer.GetString("Planning_DeleteAction"));
        await ShowModalDialogAsync(dialog);
        if (dialog.WasConfirmed)
        {
            await ViewModel.DeleteAsync(itemId);
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchText = sender.Text;
        }
    }

    private void PlanningList_DragItemsStarting(
        object sender,
        DragItemsStartingEventArgs e)
    {
        if (e.Items.FirstOrDefault() is not PlanningCardViewModel card)
        {
            e.Cancel = true;
            return;
        }

        _draggedItemId = card.Id;
        e.Data.RequestedOperation = DataPackageOperation.Move;
        e.Data.SetText(card.Id.ToString());
    }

    private void PlanningList_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedItemId.HasValue)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = _localizer.GetString("Planning_DragCaption");
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private void PlanningList_DragItemsCompleted(
        ListViewBase sender,
        DragItemsCompletedEventArgs args) =>
        _draggedItemId = null;

    private async void PlanningList_Drop(object sender, DragEventArgs e)
    {
        if (!_draggedItemId.HasValue ||
            sender is not ListView { Tag: string statusName } ||
            !Enum.TryParse(statusName, out PlanningItemStatus status))
        {
            return;
        }

        Guid itemId = _draggedItemId.Value;
        _draggedItemId = null;
        await ViewModel.MoveAsync(itemId, status);
    }

    private void PlanningErrorInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) =>
        ViewModel.ClearError();

    private Task<ContentDialogResult> ShowModalDialogAsync(ContentDialog dialog)
    {
        if (((App)Application.Current).MainWindow is not MainWindow window)
        {
            throw new InvalidOperationException(_localizer.GetString("Error_MainWindowUnavailable"));
        }

        return window.ShowModalDialogAsync(dialog, XamlRoot);
    }
}
