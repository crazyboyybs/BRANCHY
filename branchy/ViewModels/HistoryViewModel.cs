using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Loads the visual commit history and details for the selected commit.</summary>
public sealed class HistoryViewModel : ViewModelBase, IDisposable
{
    private readonly string _repositoryPath;
    private readonly IGitService _gitService;
    private readonly IArchivePickerService _archivePicker;
    private readonly ILocalizationService _localizer;
    private readonly List<GitHistoryEntry> _allCommits = [];
    private CancellationTokenSource? _detailsCancellation;
    private GitHistoryEntry? _selectedCommit;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _isLoadingDetails;
    private bool _isSearching;
    private bool _isExporting;
    private int _selectionGeneration;
    private int _searchGeneration;
    private string? _loadedCommitHash;
    private bool _hasActiveSearch;

    /// <summary>Initializes history for the active repository.</summary>
    public HistoryViewModel(
        string repositoryPath,
        IGitService gitService,
        IArchivePickerService archivePicker,
        ILocalizationService localizer)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _archivePicker = archivePicker;
        _localizer = localizer;
        _statusMessage = localizer.GetString("History_Loading");
    }

    /// <summary>Gets the commits displayed in topological order.</summary>
    public ObservableCollection<GitHistoryEntry> Commits { get; } = [];

    /// <summary>Gets the paths changed by the selected commit.</summary>
    public ObservableCollection<GitCommitFileChange> Files { get; } = [];

    /// <summary>Gets the parsed textual diff for the selected commit.</summary>
    public ObservableCollection<GitDiffLine> DiffLines { get; } = [];

    /// <summary>Gets the selected history entry.</summary>
    public GitHistoryEntry? SelectedCommit
    {
        get => _selectedCommit;
        private set
        {
            if (!SetProperty(ref _selectedCommit, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedCommit));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(SelectedSubject));
            OnPropertyChanged(nameof(SelectedHash));
            OnPropertyChanged(nameof(SelectedAuthor));
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(SelectedMessage));
            OnPropertyChanged(nameof(SelectedMessageBody));
            OnPropertyChanged(nameof(SelectedReferences));
            OnPropertyChanged(nameof(HasSelectedReferences));
            OnPropertyChanged(nameof(SelectedParentSummary));
        }
    }

    /// <summary>Gets whether the repository has commits to display.</summary>
    public bool HasCommits => Commits.Count > 0;

    /// <summary>Gets whether the repository history is empty.</summary>
    public bool HasNoCommits => !IsListLoading && !_hasActiveSearch && _allCommits.Count == 0;

    /// <summary>Gets whether the active search has no matching commits.</summary>
    public bool HasNoSearchResults => !IsListLoading && _hasActiveSearch && Commits.Count == 0;

    /// <summary>Gets whether a commit is selected.</summary>
    public bool HasSelectedCommit => SelectedCommit is not null;

    /// <summary>Gets whether the selected commit can be exported.</summary>
    public bool CanExport => HasSelectedCommit && !IsBusy;

    /// <summary>Gets the selected commit subject.</summary>
    public string SelectedSubject => SelectedCommit?.Subject ?? string.Empty;

    /// <summary>Gets the selected complete hash.</summary>
    public string SelectedHash => SelectedCommit?.Hash ?? string.Empty;

    /// <summary>Gets the selected author identity.</summary>
    public string SelectedAuthor => SelectedCommit?.AuthorIdentity ?? string.Empty;

    /// <summary>Gets the selected author date.</summary>
    public string SelectedDate => SelectedCommit?.AuthoredAt.ToString("F") ?? string.Empty;

    /// <summary>Gets the selected complete commit message.</summary>
    public string SelectedMessage => SelectedCommit?.Message ?? string.Empty;

    /// <summary>Gets the selected commit message without its subject line.</summary>
    public string SelectedMessageBody
    {
        get
        {
            string message = SelectedMessage;
            string subject = SelectedSubject;
            if (message.StartsWith(subject, StringComparison.Ordinal))
            {
                return message[subject.Length..].TrimStart('\r', '\n');
            }

            int lineBreak = message.IndexOfAny(['\r', '\n']);
            return lineBreak >= 0
                ? message[(lineBreak + 1)..].TrimStart('\r', '\n')
                : string.Empty;
        }
    }

    /// <summary>Gets the branch and tag decorations for the selected commit.</summary>
    public string SelectedReferences => SelectedCommit?.References ?? string.Empty;

    /// <summary>Gets whether the selected commit has branch or tag decorations.</summary>
    public bool HasSelectedReferences => SelectedCommit?.HasReferences == true;

    /// <summary>Gets a localized summary of the selected commit parents.</summary>
    public string SelectedParentSummary
    {
        get
        {
            IReadOnlyList<string>? parents = SelectedCommit?.ParentHashes;
            return parents?.Count switch
            {
                null => string.Empty,
                0 => _localizer.GetString("History_RootCommit"),
                1 => _localizer.Format("History_ParentCommit", parents[0][..Math.Min(7, parents[0].Length)]),
                _ => _localizer.Format("History_MergeParents", parents.Count),
            };
        }
    }

    /// <summary>Gets whether the selected commit has changed paths.</summary>
    public bool HasFiles => Files.Count > 0;

    /// <summary>Gets whether no changed paths are available.</summary>
    public bool HasNoFiles => !HasFiles;

    /// <summary>Gets whether the selected commit has a textual diff.</summary>
    public bool HasDiff => DiffLines.Count > 0;

    /// <summary>Gets whether no textual diff is available.</summary>
    public bool HasNoDiff => !HasDiff && !IsLoadingDetails;

    /// <summary>Gets the current history status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets the current Git error.</summary>
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

    /// <summary>Gets whether an error is available.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Gets whether the commit list is loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsListLoading));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(HasNoCommits));
                OnPropertyChanged(nameof(HasNoSearchResults));
            }
        }
    }

    /// <summary>Gets whether the selected commit details are loading.</summary>
    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        private set
        {
            if (SetProperty(ref _isLoadingDetails, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(HasNoDiff));
            }
        }
    }

    /// <summary>Gets whether commits are being filtered through Git.</summary>
    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (SetProperty(ref _isSearching, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsListLoading));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(HasNoCommits));
                OnPropertyChanged(nameof(HasNoSearchResults));
            }
        }
    }

    /// <summary>Gets whether the selected commit is being exported.</summary>
    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    /// <summary>Gets whether any history operation is active.</summary>
    public bool IsBusy => IsLoading || IsLoadingDetails || IsSearching || IsExporting;

    /// <summary>Gets whether the timeline is loading or applying a search.</summary>
    public bool IsListLoading => IsLoading || IsSearching;

    /// <summary>Loads the repository history and the newest commit details.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            StatusMessage = _localizer.GetString("History_Loading");
            IReadOnlyList<GitHistoryEntry> history = await _gitService.GetHistoryAsync(_repositoryPath);
            _allCommits.Clear();
            _allCommits.AddRange(history);
            _hasActiveSearch = false;
            Commits.Clear();
            foreach (GitHistoryEntry commit in history)
            {
                Commits.Add(commit);
            }

            NotifyCommitCollectionState();
            if (Commits.Count == 0)
            {
                SelectedCommit = null;
                ClearDetails();
                StatusMessage = _localizer.GetString("History_Empty");
                return;
            }

            StatusMessage = _localizer.Format("History_Loaded", Commits.Count);
            await SelectCommitAsync(Commits[0]);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("History_Failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Filters the loaded timeline by message, patch content, branch, or all fields.</summary>
    public async Task SearchAsync(
        string query,
        HistorySearchScope scope,
        CancellationToken cancellationToken = default)
    {
        int generation = ++_searchGeneration;
        string searchText = query.Trim();
        try
        {
            IsSearching = true;
            ErrorMessage = string.Empty;
            StatusMessage = _localizer.GetString("History_Searching");

            IReadOnlyList<GitHistoryEntry> matches;
            if (searchText.Length == 0)
            {
                matches = _allCommits;
            }
            else
            {
                matches = await FindMatchesAsync(searchText, scope, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _searchGeneration)
            {
                return;
            }

            GitHistoryEntry? previousSelection = SelectedCommit;
            _hasActiveSearch = searchText.Length > 0;
            Commits.Clear();
            foreach (GitHistoryEntry commit in matches)
            {
                Commits.Add(commit);
            }

            NotifyCommitCollectionState();
            GitHistoryEntry? retainedSelection = previousSelection is null
                ? null
                : Commits.FirstOrDefault(commit => commit.Hash == previousSelection.Hash);
            if (retainedSelection is not null)
            {
                SelectedCommit = retainedSelection;
            }
            else
            {
                _detailsCancellation?.Cancel();
                SelectedCommit = null;
                ClearDetails();
            }

            StatusMessage = _hasActiveSearch
                ? _localizer.Format("History_SearchResults", Commits.Count)
                : _localizer.Format("History_Loaded", Commits.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("History_SearchFailed");
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                IsSearching = false;
            }
        }
    }

    /// <summary>Loads paths and diff for one history entry.</summary>
    public async Task SelectCommitAsync(GitHistoryEntry commit)
    {
        if (SelectedCommit?.Hash == commit.Hash &&
            (string.Equals(_loadedCommitHash, commit.Hash, StringComparison.OrdinalIgnoreCase) ||
             IsLoadingDetails))
        {
            return;
        }

        int generation = ++_selectionGeneration;
        _detailsCancellation?.Cancel();
        _detailsCancellation?.Dispose();
        _detailsCancellation = new CancellationTokenSource();
        CancellationToken token = _detailsCancellation.Token;

        SelectedCommit = commit;
        ClearDetails();
        ErrorMessage = string.Empty;
        StatusMessage = _localizer.GetString("History_LoadingDetails");
        IsLoadingDetails = true;
        try
        {
            GitCommitDetails details = await _gitService.GetCommitDetailsAsync(
                _repositoryPath,
                commit.Hash,
                token);
            token.ThrowIfCancellationRequested();

            foreach (GitCommitFileChange file in details.Files)
            {
                Files.Add(file);
            }

            foreach (GitDiffLine line in GitDiffLine.Parse(details.Diff))
            {
                DiffLines.Add(line);
            }

            _loadedCommitHash = commit.Hash;
            NotifyDetailCollections();
            StatusMessage = _localizer.Format("History_CommitLoaded", Files.Count);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("History_DetailsFailed");
        }
        finally
        {
            if (generation == _selectionGeneration)
            {
                IsLoadingDetails = false;
            }
        }
    }

    /// <summary>Prompts for a ZIP path and exports the complete selected commit tree.</summary>
    public async Task ExportSelectedCommitAsync()
    {
        GitHistoryEntry? commit = SelectedCommit;
        if (commit is null || IsBusy)
        {
            return;
        }

        string repositoryName = new DirectoryInfo(_repositoryPath).Name;
        string suggestedName = SanitizeFileName($"{repositoryName}-{commit.ShortHash}");
        string? outputPath = await _archivePicker.PickArchivePathAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsExporting = true;
        StatusMessage = _localizer.GetString("History_ExportingCommit");
        try
        {
            await _gitService.ExportCommitAsync(_repositoryPath, commit.Hash, outputPath);
            StatusMessage = _localizer.Format("History_CommitExported", outputPath);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("Status_OperationFailed");
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _detailsCancellation?.Cancel();
        _detailsCancellation?.Dispose();
        _detailsCancellation = null;
    }

    private void ClearDetails()
    {
        _loadedCommitHash = null;
        Files.Clear();
        DiffLines.Clear();
        NotifyDetailCollections();
    }

    private void NotifyDetailCollections()
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasNoFiles));
        OnPropertyChanged(nameof(HasDiff));
        OnPropertyChanged(nameof(HasNoDiff));
    }

    private async Task<IReadOnlyList<GitHistoryEntry>> FindMatchesAsync(
        string query,
        HistorySearchScope scope,
        CancellationToken cancellationToken)
    {
        IEnumerable<GitHistoryEntry> messageMatches = _allCommits.Where(
            commit => commit.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        if (scope == HistorySearchScope.Message)
        {
            return messageMatches.ToList();
        }

        if (scope == HistorySearchScope.Diff)
        {
            IReadOnlySet<string> hashes = await _gitService.FindHistoryCommitsByDiffAsync(
                _repositoryPath,
                query,
                cancellationToken);
            return _allCommits.Where(commit => hashes.Contains(commit.Hash)).ToList();
        }

        if (scope == HistorySearchScope.Branch)
        {
            IReadOnlySet<string> hashes = await _gitService.FindHistoryCommitsByBranchAsync(
                _repositoryPath,
                query,
                cancellationToken);
            return _allCommits.Where(commit => hashes.Contains(commit.Hash)).ToList();
        }

        Task<IReadOnlySet<string>> diffTask = _gitService.FindHistoryCommitsByDiffAsync(
            _repositoryPath,
            query,
            cancellationToken);
        Task<IReadOnlySet<string>> branchTask = _gitService.FindHistoryCommitsByBranchAsync(
            _repositoryPath,
            query,
            cancellationToken);
        IReadOnlySet<string>[] results = await Task.WhenAll(diffTask, branchTask);

        var matchedHashes = new HashSet<string>(results[0], StringComparer.OrdinalIgnoreCase);
        matchedHashes.UnionWith(results[1]);
        matchedHashes.UnionWith(messageMatches.Select(commit => commit.Hash));
        return _allCommits.Where(commit => matchedHashes.Contains(commit.Hash)).ToList();
    }

    private void NotifyCommitCollectionState()
    {
        OnPropertyChanged(nameof(HasCommits));
        OnPropertyChanged(nameof(HasNoCommits));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
