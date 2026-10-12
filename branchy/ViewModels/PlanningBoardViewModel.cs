using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates a repository planning board, filters, summaries, and persistence.</summary>
public sealed class PlanningBoardViewModel : ViewModelBase
{
    private readonly IPlanningBoardService _boardService;
    private readonly ILocalizationService _localizer;
    private PlanningBoard _board = new();
    private string _repositoryPath = string.Empty;
    private string _repositoryName = string.Empty;
    private string _searchText = string.Empty;
    private PlanningTypeFilterOption? _selectedTypeFilter;
    private PlanningTagFilterOption? _selectedTagFilter;
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    /// <summary>Initializes the planning board ViewModel.</summary>
    public PlanningBoardViewModel(
        IPlanningBoardService boardService,
        ILocalizationService localizer)
    {
        _boardService = boardService;
        _localizer = localizer;
        TypeFilters =
        [
            new PlanningTypeFilterOption(null, localizer.GetString("Planning_FilterAllTypes")),
            new PlanningTypeFilterOption(PlanningItemType.Feature, GetTypeLabel(PlanningItemType.Feature)),
            new PlanningTypeFilterOption(PlanningItemType.Bug, GetTypeLabel(PlanningItemType.Bug)),
            new PlanningTypeFilterOption(PlanningItemType.Implementation, GetTypeLabel(PlanningItemType.Implementation)),
            new PlanningTypeFilterOption(PlanningItemType.Improvement, GetTypeLabel(PlanningItemType.Improvement)),
        ];
        _selectedTypeFilter = TypeFilters[0];
        TagFilters.Add(new PlanningTagFilterOption(null, localizer.GetString("Planning_FilterAllTags")));
        _selectedTagFilter = TagFilters[0];
    }

    /// <summary>Gets the items currently visible in the ideas column.</summary>
    public ObservableCollection<PlanningCardViewModel> Ideas { get; } = [];

    /// <summary>Gets the items currently visible in the planned column.</summary>
    public ObservableCollection<PlanningCardViewModel> Planned { get; } = [];

    /// <summary>Gets the items currently visible in the in-progress column.</summary>
    public ObservableCollection<PlanningCardViewModel> InProgress { get; } = [];

    /// <summary>Gets the items currently visible in the completed column.</summary>
    public ObservableCollection<PlanningCardViewModel> Completed { get; } = [];

    /// <summary>Gets the available item-type filters.</summary>
    public IReadOnlyList<PlanningTypeFilterOption> TypeFilters { get; }

    /// <summary>Gets the tags available for filtering.</summary>
    public ObservableCollection<PlanningTagFilterOption> TagFilters { get; } = [];

    /// <summary>Gets the repository name displayed by the page.</summary>
    public string RepositoryName
    {
        get => _repositoryName;
        private set => SetProperty(ref _repositoryName, value);
    }

    /// <summary>Gets or sets the current text filter.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshVisibleItems();
            }
        }
    }

    /// <summary>Gets or sets the current type filter.</summary>
    public PlanningTypeFilterOption? SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                RefreshVisibleItems();
            }
        }
    }

    /// <summary>Gets or sets the current tag filter.</summary>
    public PlanningTagFilterOption? SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            if (SetProperty(ref _selectedTagFilter, value))
            {
                RefreshVisibleItems();
            }
        }
    }

    /// <summary>Gets whether the board is loading or saving.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Gets the latest persistence error.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Gets whether an error should be displayed.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Gets the localized ideas-column header.</summary>
    public string IdeasHeader => _localizer.Format("Planning_ColumnIdeas", Ideas.Count);

    /// <summary>Gets the localized planned-column header.</summary>
    public string PlannedHeader => _localizer.Format("Planning_ColumnPlanned", Planned.Count);

    /// <summary>Gets the localized in-progress-column header.</summary>
    public string InProgressHeader => _localizer.Format("Planning_ColumnInProgress", InProgress.Count);

    /// <summary>Gets the localized completed-column header.</summary>
    public string CompletedHeader => _localizer.Format("Planning_ColumnCompleted", Completed.Count);

    /// <summary>Gets the number of stored items.</summary>
    public int TotalCount => _board.Items.Count;

    /// <summary>Gets the number of stored in-progress items.</summary>
    public int InProgressCount => _board.Items.Count(item => item.Status == PlanningItemStatus.InProgress);

    /// <summary>Gets the number of stored completed items.</summary>
    public int CompletedCount => _board.Items.Count(item => item.Status == PlanningItemStatus.Completed);

    /// <summary>Gets average completion across the board.</summary>
    public int AverageProgress => _board.Items.Count == 0
        ? 0
        : (int)Math.Round(_board.Items.Average(item => item.Progress));

    /// <summary>Gets whether the filtered ideas column is empty.</summary>
    public bool HasNoIdeas => Ideas.Count == 0;

    /// <summary>Gets whether the filtered planned column is empty.</summary>
    public bool HasNoPlanned => Planned.Count == 0;

    /// <summary>Gets whether the filtered in-progress column is empty.</summary>
    public bool HasNoInProgress => InProgress.Count == 0;

    /// <summary>Gets whether the filtered completed column is empty.</summary>
    public bool HasNoCompleted => Completed.Count == 0;

    /// <summary>Gets the localized total-items summary.</summary>
    public string TotalSummary => _localizer.Format("Planning_SummaryTotal", TotalCount);

    /// <summary>Gets the localized in-progress summary.</summary>
    public string InProgressSummary => _localizer.Format("Planning_SummaryInProgress", InProgressCount);

    /// <summary>Gets the localized completed summary.</summary>
    public string CompletedSummary => _localizer.Format("Planning_SummaryCompleted", CompletedCount);

    /// <summary>Gets the localized average progress summary.</summary>
    public string ProgressSummary => _localizer.Format("Planning_SummaryProgress", AverageProgress);

    /// <summary>Loads a repository board.</summary>
    public async Task LoadAsync(string repositoryPath, string repositoryName)
    {
        _repositoryPath = repositoryPath;
        RepositoryName = repositoryName;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            _board = await _boardService.LoadAsync(repositoryPath);
            NormalizeBoard();
            RefreshTagFilters();
            RefreshVisibleItems();
        }
        catch (Exception exception)
        {
            ErrorMessage = _localizer.Format("Planning_ErrorLoad", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Adds a new planning item and saves the board.</summary>
    public async Task AddAsync(PlanningItem item)
    {
        item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
        item.CreatedAt = DateTimeOffset.UtcNow;
        item.UpdatedAt = item.CreatedAt;
        NormalizeItem(item);
        _board.Items.Add(item);
        await SaveAndRefreshAsync();
    }

    /// <summary>Updates an existing planning item and saves the board.</summary>
    public async Task UpdateAsync(PlanningItem item)
    {
        int index = _board.Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0)
        {
            return;
        }

        item.CreatedAt = _board.Items[index].CreatedAt;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        NormalizeItem(item);
        _board.Items[index] = item;
        await SaveAndRefreshAsync();
    }

    /// <summary>Deletes a planning item and saves the board.</summary>
    public async Task DeleteAsync(Guid itemId)
    {
        _board.Items.RemoveAll(item => item.Id == itemId);
        await SaveAndRefreshAsync();
    }

    /// <summary>Moves an item to another workflow column and saves the board.</summary>
    public async Task MoveAsync(Guid itemId, PlanningItemStatus targetStatus)
    {
        PlanningItem? item = _board.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null || item.Status == targetStatus)
        {
            return;
        }

        item.Status = targetStatus;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        if (targetStatus == PlanningItemStatus.Completed)
        {
            item.Progress = 100;
        }

        await SaveAndRefreshAsync();
    }

    /// <summary>Finds a stored item and returns a detached editable copy.</summary>
    public PlanningItem? CreateEditableCopy(Guid itemId)
    {
        PlanningItem? item = _board.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        return item is null ? null : Clone(item);
    }

    /// <summary>Dismisses the currently displayed error.</summary>
    public void ClearError() => ErrorMessage = string.Empty;

    private async Task SaveAndRefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _boardService.SaveAsync(_repositoryPath, _board);
            RefreshTagFilters();
            RefreshVisibleItems();
        }
        catch (Exception exception)
        {
            ErrorMessage = _localizer.Format("Planning_ErrorSave", exception.Message);
            RefreshVisibleItems();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshVisibleItems()
    {
        Ideas.Clear();
        Planned.Clear();
        InProgress.Clear();
        Completed.Clear();

        foreach (PlanningItem item in _board.Items
                     .Where(MatchesFilters)
                     .OrderByDescending(item => item.UpdatedAt))
        {
            GetColumn(item.Status).Add(new PlanningCardViewModel(item, _localizer));
        }

        RaiseBoardPropertiesChanged();
    }

    private bool MatchesFilters(PlanningItem item)
    {
        if (SelectedTypeFilter?.Type is PlanningItemType type && item.Type != type)
        {
            return false;
        }

        if (SelectedTagFilter?.Tag is string tag &&
            !item.Tags.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
        {
            return false;
        }

        string query = SearchText.Trim();
        return query.Length == 0 ||
            item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.Documentation.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.Tags.Any(tagValue => tagValue.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
            item.LinkedCommitSubject.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.LinkedCommitShortHash.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private ObservableCollection<PlanningCardViewModel> GetColumn(PlanningItemStatus status) =>
        status switch
        {
            PlanningItemStatus.Planned => Planned,
            PlanningItemStatus.InProgress => InProgress,
            PlanningItemStatus.Completed => Completed,
            _ => Ideas,
        };

    private void RefreshTagFilters()
    {
        string? selectedTag = SelectedTagFilter?.Tag;
        TagFilters.Clear();
        TagFilters.Add(new PlanningTagFilterOption(null, _localizer.GetString("Planning_FilterAllTags")));
        foreach (string tag in _board.Items
                     .SelectMany(item => item.Tags)
                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase))
        {
            TagFilters.Add(new PlanningTagFilterOption(tag, tag));
        }

        PlanningTagFilterOption nextSelection = TagFilters.FirstOrDefault(option =>
            string.Equals(option.Tag, selectedTag, StringComparison.CurrentCultureIgnoreCase)) ?? TagFilters[0];
        if (!ReferenceEquals(_selectedTagFilter, nextSelection))
        {
            _selectedTagFilter = nextSelection;
            OnPropertyChanged(nameof(SelectedTagFilter));
        }
    }

    private void NormalizeBoard()
    {
        _board.Items ??= [];
        foreach (PlanningItem item in _board.Items)
        {
            NormalizeItem(item);
        }
    }

    private static void NormalizeItem(PlanningItem item)
    {
        item.Title = item.Title.Trim();
        item.Description = item.Description.Trim();
        item.Documentation = item.Documentation.Trim();
        item.Progress = Math.Clamp(item.Progress, 0, 100);
        item.Tags = item.Tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void RaiseBoardPropertiesChanged()
    {
        OnPropertyChanged(nameof(IdeasHeader));
        OnPropertyChanged(nameof(PlannedHeader));
        OnPropertyChanged(nameof(InProgressHeader));
        OnPropertyChanged(nameof(CompletedHeader));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(InProgressCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(AverageProgress));
        OnPropertyChanged(nameof(HasNoIdeas));
        OnPropertyChanged(nameof(HasNoPlanned));
        OnPropertyChanged(nameof(HasNoInProgress));
        OnPropertyChanged(nameof(HasNoCompleted));
        OnPropertyChanged(nameof(TotalSummary));
        OnPropertyChanged(nameof(InProgressSummary));
        OnPropertyChanged(nameof(CompletedSummary));
        OnPropertyChanged(nameof(ProgressSummary));
    }

    private string GetTypeLabel(PlanningItemType type) =>
        _localizer.GetString($"Planning_Type_{type}");

    private static PlanningItem Clone(PlanningItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Documentation = item.Documentation,
        Type = item.Type,
        Status = item.Status,
        Progress = item.Progress,
        Tags = [.. item.Tags],
        LinkedCommitHash = item.LinkedCommitHash,
        LinkedCommitShortHash = item.LinkedCommitShortHash,
        LinkedCommitSubject = item.LinkedCommitSubject,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
    };
}

/// <summary>Presents one planning item to compiled XAML bindings.</summary>
public sealed class PlanningCardViewModel
{
    private readonly PlanningItem _item;

    /// <summary>Initializes a localized planning card.</summary>
    public PlanningCardViewModel(PlanningItem item, ILocalizationService localizer)
    {
        _item = item;
        TypeLabel = localizer.GetString($"Planning_Type_{item.Type}");
        DocumentationHeader = localizer.GetString("Planning_DocumentationHeader");
        ProgressText = localizer.Format("Planning_ProgressValue", item.Progress);
        Tags = item.Tags.Select(tag => new PlanningTagViewModel(tag)).ToArray();
    }

    /// <summary>Gets the stable item identifier.</summary>
    public Guid Id => _item.Id;

    /// <summary>Gets the title.</summary>
    public string Title => _item.Title;

    /// <summary>Gets the short description.</summary>
    public string Description => _item.Description;

    /// <summary>Gets the detailed documentation.</summary>
    public string Documentation => _item.Documentation;

    /// <summary>Gets the item kind.</summary>
    public PlanningItemType Type => _item.Type;

    /// <summary>Gets the localized item kind.</summary>
    public string TypeLabel { get; }

    /// <summary>Gets completion progress.</summary>
    public int Progress => _item.Progress;

    /// <summary>Gets localized completion progress.</summary>
    public string ProgressText { get; }

    /// <summary>Gets the user-created tags.</summary>
    public IReadOnlyList<PlanningTagViewModel> Tags { get; }

    /// <summary>Gets whether a short description is available.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>Gets whether detailed documentation is available.</summary>
    public bool HasDocumentation => !string.IsNullOrWhiteSpace(Documentation);

    /// <summary>Gets whether a commit is associated with this item.</summary>
    public bool HasLinkedCommit => !string.IsNullOrWhiteSpace(_item.LinkedCommitHash);

    /// <summary>Gets the associated commit summary.</summary>
    public string LinkedCommitSummary => HasLinkedCommit
        ? $"{_item.LinkedCommitShortHash} · {_item.LinkedCommitSubject}"
        : string.Empty;

    /// <summary>Gets the localized documentation expander header.</summary>
    public string DocumentationHeader { get; }
}

/// <summary>Presents one user-created tag.</summary>
public sealed class PlanningTagViewModel
{
    /// <summary>Initializes a tag for XAML activation.</summary>
    public PlanningTagViewModel()
    {
    }

    /// <summary>Initializes a tag.</summary>
    public PlanningTagViewModel(string label) => Label = label;

    /// <summary>Gets or sets the displayed tag label.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>Presents an optional planning item type filter.</summary>
public sealed record PlanningTypeFilterOption(PlanningItemType? Type, string Label);

/// <summary>Presents an optional planning tag filter.</summary>
public sealed record PlanningTagFilterOption(string? Tag, string Label);
