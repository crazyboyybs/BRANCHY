using System.Collections.ObjectModel;
using System.Windows.Input;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates repository tasks for the main workspace.</summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IGitService _gitService;
    private readonly IRepositoryPickerService _repositoryPicker;
    private readonly ILocalizationService _localizer;
    private readonly AsyncRelayCommand _openRepositoryCommand;
    private readonly AsyncRelayCommand _initializeRepositoryCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _stageCommand;
    private readonly AsyncRelayCommand _unstageCommand;
    private readonly AsyncRelayCommand _toggleAllChangesCommand;
    private readonly AsyncRelayCommand _commitCommand;
    private readonly AsyncRelayCommand _fetchCommand;
    private readonly AsyncRelayCommand _pullCommand;
    private readonly AsyncRelayCommand _pushCommand;
    private readonly AsyncRelayCommand _switchBranchCommand;
    private readonly AsyncRelayCommand _createBranchCommand;
    private readonly AsyncRelayCommand _renameBranchCommand;
    private readonly AsyncRelayCommand _deleteBranchCommand;
    private CancellationTokenSource? _diffCancellation;
    private string _repositoryPath = string.Empty;
    private string _suggestedRepositoryPath = string.Empty;
    private string _repositoryName = string.Empty;
    private string _branchName = string.Empty;
    private string _headOid = string.Empty;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private string _selectedFilePath = string.Empty;
    private GitFileChange? _selectedChange;
    private IReadOnlyList<GitDiffLine> _diffLines = [];
    private string _commitMessage = string.Empty;
    private GitBranch? _selectedBranch;
    private string _newBranchName = string.Empty;
    private string _renamedBranchName = string.Empty;
    private int _changesTabIndex;
    private int _conflictCount;
    private bool _isBusy;
    private bool _initialized;

    /// <summary>Initializes services and application commands.</summary>
    public MainViewModel(
        IGitService gitService,
        IRepositoryPickerService repositoryPicker,
        ILocalizationService localizer)
    {
        _gitService = gitService;
        _repositoryPicker = repositoryPicker;
        _localizer = localizer;
        _statusMessage = localizer.GetString("Status_OpenRepository");

        Action<Exception> onCommandError = ShowUnexpectedError;
        _openRepositoryCommand = new AsyncRelayCommand(_ => OpenRepositoryAsync(), onError: onCommandError);
        _initializeRepositoryCommand = new AsyncRelayCommand(
            _ => InitializeRepositoryAsync(),
            _ => CanInitializeRepository(),
            onCommandError);
        _refreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => CanUseRepository(), onCommandError);
        _stageCommand = new AsyncRelayCommand(StageAsync, CanUseFile, onCommandError);
        _unstageCommand = new AsyncRelayCommand(UnstageAsync, CanUseFile, onCommandError);
        _toggleAllChangesCommand = new AsyncRelayCommand(
            _ => ToggleAllChangesAsync(),
            _ => CanToggleAllChanges(),
            onCommandError);
        _commitCommand = new AsyncRelayCommand(_ => CommitAsync(), _ => CanCommit(), onCommandError);
        _fetchCommand = new AsyncRelayCommand(_ => FetchAsync(), _ => CanUseRepository(), onCommandError);
        _pullCommand = new AsyncRelayCommand(_ => PullAsync(), _ => CanUseRepository(), onCommandError);
        _pushCommand = new AsyncRelayCommand(_ => PushAsync(), _ => CanUseRepository(), onCommandError);
        _switchBranchCommand = new AsyncRelayCommand(_ => SwitchBranchAsync(), _ => CanSwitchBranch(), onCommandError);
        _createBranchCommand = new AsyncRelayCommand(_ => CreateBranchAsync(), _ => CanCreateBranch(), onCommandError);
        _renameBranchCommand = new AsyncRelayCommand(_ => RenameBranchAsync(), _ => CanRenameBranch(), onCommandError);
        _deleteBranchCommand = new AsyncRelayCommand(_ => DeleteBranchAsync(), _ => CanDeleteBranch(), onCommandError);
    }

    /// <summary>Gets the files that still contain working-tree changes.</summary>
    public ObservableCollection<GitFileChange> UnstagedFiles { get; } = [];

    /// <summary>Gets the files already present in the index.</summary>
    public ObservableCollection<GitFileChange> StagedFiles { get; } = [];

    /// <summary>Gets the local branches available in the active repository.</summary>
    public ObservableCollection<GitBranch> Branches { get; } = [];

    /// <summary>Gets the semantic lines shown by the change preview.</summary>
    public IReadOnlyList<GitDiffLine> DiffLines
    {
        get => _diffLines;
        private set => SetProperty(ref _diffLines, value);
    }

    /// <summary>Gets whether a selected working-tree file is available for quick editing.</summary>
    public bool CanQuickEdit => HasRepository && !IsBusy && _selectedChange is { IsWorkTreeDeletion: false };

    /// <summary>Gets the branch action description including the current branch.</summary>
    public string SwitchBranchToolTip => _localizer.Format("Branch_SwitchTooltip", BranchName);

    /// <summary>Gets the active repository root.</summary>
    public string RepositoryPath
    {
        get => _repositoryPath;
        private set => SetProperty(ref _repositoryPath, value);
    }

    /// <summary>Gets the folder offered for Git repository initialization.</summary>
    public string SuggestedRepositoryPath
    {
        get => _suggestedRepositoryPath;
        private set
        {
            if (SetProperty(ref _suggestedRepositoryPath, value))
            {
                OnPropertyChanged(nameof(HasRepositorySuggestion));
                OnPropertyChanged(nameof(HasNoRepositorySuggestion));
                _initializeRepositoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the active repository folder name.</summary>
    public string RepositoryName
    {
        get => _repositoryName;
        private set => SetProperty(ref _repositoryName, value);
    }

    /// <summary>Gets the current branch name or a localized detached-HEAD label.</summary>
    public string BranchName
    {
        get => _branchName;
        private set
        {
            if (SetProperty(ref _branchName, value)) OnPropertyChanged(nameof(SwitchBranchToolTip));
        }
    }

    /// <summary>Gets the current operation summary.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets the most recent actionable error.</summary>
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

    /// <summary>Gets whether a diff is available for display.</summary>
    public bool HasDiff => DiffLines.Count > 0;

    /// <summary>Gets whether the diff viewer should show its empty state.</summary>
    public bool HasNoDiff => !HasDiff;

    /// <summary>Gets or sets the selected changes tab.</summary>
    public int ChangesTabIndex
    {
        get => _changesTabIndex;
        set => SetProperty(ref _changesTabIndex, value);
    }

    /// <summary>Gets the path currently shown in the diff viewer.</summary>
    public string SelectedFilePath
    {
        get => _selectedFilePath;
        private set => SetProperty(ref _selectedFilePath, value);
    }

    /// <summary>Gets or sets the pending commit message.</summary>
    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (SetProperty(ref _commitMessage, value))
            {
                _commitCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets or sets the branch selected in the branch manager.</summary>
    public GitBranch? SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            if (SetProperty(ref _selectedBranch, value))
            {
                RenamedBranchName = value?.Name ?? string.Empty;
                NotifyBranchCommands();
            }
        }
    }

    /// <summary>Gets or sets the name of a branch to create.</summary>
    public string NewBranchName
    {
        get => _newBranchName;
        set
        {
            if (SetProperty(ref _newBranchName, value))
            {
                _createBranchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets or sets the replacement name for the selected branch.</summary>
    public string RenamedBranchName
    {
        get => _renamedBranchName;
        set
        {
            if (SetProperty(ref _renamedBranchName, value))
            {
                _renameBranchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets whether a repository is active.</summary>
    public bool HasRepository => !string.IsNullOrWhiteSpace(RepositoryPath);

    /// <summary>Gets whether the repository picker empty state should be displayed.</summary>
    public bool HasNoRepository => !HasRepository;

    /// <summary>Gets whether the Explorer folder can be initialized as a repository.</summary>
    public bool HasRepositorySuggestion => !string.IsNullOrWhiteSpace(SuggestedRepositoryPath);

    /// <summary>Gets whether the normal repository picker empty state should be shown.</summary>
    public bool HasNoRepositorySuggestion => !HasRepositorySuggestion;

    /// <summary>Gets whether no working-tree changes are pending.</summary>
    public bool HasNoUnstagedFiles => UnstagedFiles.Count == 0;

    /// <summary>Gets whether no files are staged.</summary>
    public bool HasNoStagedFiles => StagedFiles.Count == 0;

    /// <summary>Gets whether every pending change is currently present in the index.</summary>
    public bool AreAllChangesStaged => UnstagedFiles.Count == 0 && StagedFiles.Count > 0;

    /// <summary>Gets whether the active repository already contains commits.</summary>
    public bool HasCommits => !string.IsNullOrWhiteSpace(_headOid) && _headOid != "(initial)";

    /// <summary>Gets the number of paths with unresolved conflicts.</summary>
    public int ConflictCount
    {
        get => _conflictCount;
        private set
        {
            if (SetProperty(ref _conflictCount, value))
            {
                OnPropertyChanged(nameof(HasConflicts));
                OnPropertyChanged(nameof(ConflictMessage));
                OnPropertyChanged(nameof(CanAmend));
                OnPropertyChanged(nameof(CanAmendMessage));
                OnPropertyChanged(nameof(CanMergeSelectedBranch));
            }
        }
    }

    /// <summary>Gets whether unresolved conflicts are present.</summary>
    public bool HasConflicts => ConflictCount > 0;

    /// <summary>Gets the localized conflict summary displayed above the workspace.</summary>
    public string ConflictMessage => _localizer.Format("Conflict_Summary", ConflictCount);

    /// <summary>Gets whether the amend action should be visible.</summary>
    public bool CanShowAmend => HasRepository && HasCommits && StagedFiles.Count > 0;

    /// <summary>Gets whether the amend dialog can be opened now.</summary>
    public bool CanAmend => CanShowAmend && !HasConflicts && !IsBusy;

    /// <summary>Gets whether the message-only amend action should be visible.</summary>
    public bool CanShowAmendMessage => HasRepository && HasCommits;

    /// <summary>Gets whether a commit message can be rewritten now.</summary>
    public bool CanAmendMessage => CanShowAmendMessage && !HasConflicts && !IsBusy;

    /// <summary>Gets whether the selected branch can be merged into the current branch.</summary>
    public bool CanMergeSelectedBranch =>
        HasRepository && !HasConflicts && !IsBusy && SelectedBranch is { IsCurrent: false };

    /// <summary>Gets the localized action presented by the all-changes toggle.</summary>
    public string ToggleAllChangesLabel => _localizer.GetString(
        AreAllChangesStaged ? "StageSelection_ClearAll" : "StageSelection_SelectAll");

    /// <summary>Gets whether a Git operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanQuickEdit));
                OnPropertyChanged(nameof(CanAmend));
                OnPropertyChanged(nameof(CanAmendMessage));
                OnPropertyChanged(nameof(CanMergeSelectedBranch));
                NotifyCommands();
            }
        }
    }

    /// <summary>Gets the command that opens a repository.</summary>
    public ICommand OpenRepositoryCommand => _openRepositoryCommand;

    /// <summary>Gets the command that initializes the Explorer folder as a repository.</summary>
    public ICommand InitializeRepositoryCommand => _initializeRepositoryCommand;

    /// <summary>Gets the command that refreshes repository state.</summary>
    public ICommand RefreshCommand => _refreshCommand;

    /// <summary>Gets the command that stages a file.</summary>
    public ICommand StageCommand => _stageCommand;

    /// <summary>Gets the command that unstages a file.</summary>
    public ICommand UnstageCommand => _unstageCommand;

    /// <summary>Gets the command that stages or unstages every change.</summary>
    public ICommand ToggleAllChangesCommand => _toggleAllChangesCommand;

    /// <summary>Gets the command that creates a commit.</summary>
    public ICommand CommitCommand => _commitCommand;

    /// <summary>Gets the command that fetches remotes.</summary>
    public ICommand FetchCommand => _fetchCommand;

    /// <summary>Gets the command that fast-forwards the current branch.</summary>
    public ICommand PullCommand => _pullCommand;

    /// <summary>Gets the command that pushes the current branch.</summary>
    public ICommand PushCommand => _pushCommand;

    /// <summary>Gets the command that switches to the selected branch.</summary>
    public ICommand SwitchBranchCommand => _switchBranchCommand;

    /// <summary>Gets the command that creates and switches to a branch.</summary>
    public ICommand CreateBranchCommand => _createBranchCommand;

    /// <summary>Gets the command that renames the selected branch.</summary>
    public ICommand RenameBranchCommand => _renameBranchCommand;

    /// <summary>Gets the command that safely deletes the selected branch.</summary>
    public ICommand DeleteBranchCommand => _deleteBranchCommand;

    /// <summary>Attempts to open the process working directory on first load.</summary>
    public async Task InitializeAsync(string initialPath, bool wasRequestedByExplorer)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            string root = await _gitService.FindRepositoryRootAsync(initialPath);
            await LoadRepositoryAsync(root);
        }
        catch (GitRepositoryNotFoundException) when (
            wasRequestedByExplorer && Directory.Exists(initialPath))
        {
            SuggestedRepositoryPath = Path.GetFullPath(initialPath);
            ErrorMessage = string.Empty;
            StatusMessage = _localizer.GetString("Status_RepositoryNotInitialized");
        }
        catch (GitRepositoryNotFoundException)
        {
            ErrorMessage = string.Empty;
            StatusMessage = _localizer.GetString("Status_OpenRepository");
        }
        catch (Exception exception)
        {
            ShowUnexpectedError(exception);
        }
    }

    /// <summary>Loads the diff for the selected file and area.</summary>
    public async Task SelectFileAsync(GitFileChange change, bool staged)
    {
        if (!HasRepository || IsBusy)
        {
            return;
        }

        _diffCancellation?.Cancel();
        _diffCancellation?.Dispose();
        _diffCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _diffCancellation.Token;
        string repositoryPath = RepositoryPath;
        _selectedChange = change;
        SelectedFilePath = change.Path;
        OnPropertyChanged(nameof(CanQuickEdit));
        ErrorMessage = string.Empty;

        try
        {
            string diff = await _gitService.GetDiffAsync(
                repositoryPath,
                change,
                staged,
                cancellationToken);
            IReadOnlyList<GitDiffLine> lines = await Task.Run(() => GitDiffLine.Parse(diff), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (RepositoryPath != repositoryPath) return;
            DiffLines = lines;
            NotifyDiffState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowUnexpectedError(exception);
        }
    }

    /// <summary>Refreshes repository state after a modal repository operation.</summary>
    public Task RefreshAsyncFromDialog() => HasRepository ? RefreshAsync() : Task.CompletedTask;

    /// <summary>Restores a tracked working-tree path from the index.</summary>
    public Task DiscardWorkingTreeChangeAsync(GitFileChange change)
    {
        if (!change.CanDiscardWorkingTreeChange || !CanUseRepository())
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                await _gitService.DiscardWorkingTreeChangeAsync(
                    RepositoryPath,
                    change.Path);
                await RefreshCoreAsync();
                StatusMessage = _localizer.Format(
                    change.IsWorkTreeDeletion
                        ? "Status_FileRestored"
                        : "Status_ChangesDiscarded",
                    change.Path);
            },
            change.IsWorkTreeDeletion
                ? "Status_RestoringFile"
                : "Status_DiscardingChanges",
            change.Path);
    }

    /// <summary>Opens a repository produced by an external modal workflow such as clone.</summary>
    public Task OpenRepositoryPathAsync(string path)
    {
        SuggestedRepositoryPath = string.Empty;
        return LoadRepositoryAsync(path);
    }

    /// <summary>Adds the staged changes to the selected commit.</summary>
    public Task AmendAsync(GitCommit commit, string message)
    {
        if (!CanAmend)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                await _gitService.AmendCommitAsync(RepositoryPath, commit.Hash, message);
                CommitMessage = string.Empty;
                await RefreshCoreAsync();
                StatusMessage = _localizer.GetString("Status_AmendComplete");
            },
            "Status_Amending");
    }

    /// <summary>Rewrites only the selected commit message.</summary>
    public Task AmendMessageAsync(GitCommit commit, string message)
    {
        if (!CanAmendMessage)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                await _gitService.AmendCommitMessageAsync(
                    RepositoryPath,
                    commit.Hash,
                    message);
                await RefreshCoreAsync();
                StatusMessage = _localizer.GetString("Status_AmendMessageComplete");
            },
            "Status_AmendingMessage");
    }

    /// <summary>Merges the selected local branch into the current branch.</summary>
    public Task MergeBranchAsync(GitBranch branch)
    {
        if (!HasRepository || HasConflicts || IsBusy || branch.IsCurrent)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                GitOperationResult result = await _gitService.MergeBranchAsync(
                    RepositoryPath,
                    branch.Name);
                await RefreshCoreAsync();
                StatusMessage = result.HasConflicts
                    ? _localizer.Format("Status_MergeConflicts", result.ConflictedPaths.Count)
                    : _localizer.Format("Status_MergeComplete", branch.Name);
            },
            "Status_MergingBranch",
            branch.Name);
    }

    /// <summary>Reverts the selected commit and reports conflicts without discarding them.</summary>
    public Task RevertCommitAsync(GitHistoryEntry commit)
    {
        if (!HasRepository || HasConflicts || IsBusy)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                GitOperationResult result = await _gitService.RevertCommitAsync(
                    RepositoryPath,
                    commit.Hash);
                await RefreshCoreAsync();
                StatusMessage = result.HasConflicts
                    ? _localizer.Format("Status_RevertConflicts", result.ConflictedPaths.Count)
                    : _localizer.Format("Status_RevertComplete", commit.ShortHash);
            },
            "Status_RevertingCommit",
            commit.ShortHash);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _diffCancellation?.Cancel();
        _diffCancellation?.Dispose();
        _diffCancellation = null;
    }

    private async Task OpenRepositoryAsync()
    {
        string? path = await _repositoryPicker.PickRepositoryAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SuggestedRepositoryPath = string.Empty;
            await LoadRepositoryAsync(path);
        }
    }

    private Task InitializeRepositoryAsync() => RunBusyAsync(
        async () =>
        {
            string path = SuggestedRepositoryPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await _gitService.InitializeRepositoryAsync(path);
            SuggestedRepositoryPath = string.Empty;
            RepositoryPath = await _gitService.FindRepositoryRootAsync(path);
            await RefreshCoreAsync();
            StatusMessage = _localizer.GetString("Status_RepositoryInitialized");
        },
        "Status_InitializingRepository");

    private Task LoadRepositoryAsync(string path) => RunBusyAsync(
        async () =>
        {
            RepositoryPath = await _gitService.FindRepositoryRootAsync(path);
            await RefreshCoreAsync();
        },
        "Status_LoadingRepository");

    private Task RefreshAsync() => RunBusyAsync(
        RefreshCoreAsync,
        "Status_Refreshing");

    private async Task RefreshCoreAsync()
    {
        _diffCancellation?.Cancel();
        GitRepositorySnapshot snapshot = await _gitService.GetSnapshotAsync(RepositoryPath);
        RepositoryName = snapshot.RepositoryName;
        BranchName = string.IsNullOrWhiteSpace(snapshot.BranchName)
            ? _localizer.GetString("Branch_Detached")
            : snapshot.BranchName;
        _headOid = snapshot.HeadOid;

        IReadOnlyList<GitBranch> branches = await _gitService.GetBranchesAsync(RepositoryPath);
        string? selectedName = SelectedBranch?.Name;
        Branches.Clear();
        foreach (GitBranch branch in branches)
        {
            Branches.Add(branch);
        }

        SelectedBranch = Branches.FirstOrDefault(branch => branch.Name == selectedName)
            ?? Branches.FirstOrDefault(branch => branch.IsCurrent)
            ?? Branches.FirstOrDefault();

        UnstagedFiles.Clear();
        StagedFiles.Clear();
        ConflictCount = snapshot.Changes.Count(change => change.IsConflicted);
        foreach (GitFileChange change in snapshot.Changes)
        {
            if (change.HasWorkingTreeChange)
            {
                UnstagedFiles.Add(change);
            }

            if (change.IsStaged)
            {
                StagedFiles.Add(change);
            }
        }

        ClearDiff();
        SelectedFilePath = string.Empty;
        StatusMessage = _localizer.Format(
            "Status_ReadySummary",
            UnstagedFiles.Count,
            StagedFiles.Count);
        OnPropertyChanged(nameof(HasRepository));
        OnPropertyChanged(nameof(HasNoRepository));
        OnPropertyChanged(nameof(HasNoUnstagedFiles));
        OnPropertyChanged(nameof(HasNoStagedFiles));
        OnPropertyChanged(nameof(AreAllChangesStaged));
        OnPropertyChanged(nameof(HasCommits));
        OnPropertyChanged(nameof(CanShowAmend));
        OnPropertyChanged(nameof(CanAmend));
        OnPropertyChanged(nameof(CanShowAmendMessage));
        OnPropertyChanged(nameof(CanAmendMessage));
        OnPropertyChanged(nameof(CanMergeSelectedBranch));
        OnPropertyChanged(nameof(ToggleAllChangesLabel));
        NotifyCommands();
    }

    private async Task StageAsync(object? parameter)
    {
        if (parameter is not GitFileChange change)
        {
            return;
        }

        await RunBusyAsync(
            async () =>
            {
                await _gitService.StageAsync(RepositoryPath, change.Path);
                await RefreshCoreAsync();
            },
            "Status_StagingFile",
            change.Path);
    }

    private async Task UnstageAsync(object? parameter)
    {
        if (parameter is not GitFileChange change)
        {
            return;
        }

        await RunBusyAsync(
            async () =>
            {
                bool hasHead = !string.IsNullOrWhiteSpace(_headOid) && _headOid != "(initial)";
                await _gitService.UnstageAsync(RepositoryPath, change.Path, hasHead);
                await RefreshCoreAsync();
            },
            "Status_UnstagingFile",
            change.Path);
    }

    private Task ToggleAllChangesAsync()
    {
        bool shouldUnstage = AreAllChangesStaged;
        return RunBusyAsync(
            async () =>
            {
                if (shouldUnstage)
                {
                    bool hasHead = !string.IsNullOrWhiteSpace(_headOid) && _headOid != "(initial)";
                    await _gitService.UnstageAllAsync(RepositoryPath, hasHead);
                }
                else
                {
                    await _gitService.StageAllAsync(RepositoryPath);
                }

                await RefreshCoreAsync();
                ChangesTabIndex = shouldUnstage ? 0 : 1;
            },
            shouldUnstage ? "Status_UnstagingAll" : "Status_StagingAll");
    }

    private Task CommitAsync() => RunBusyAsync(
        async () =>
        {
            if (string.IsNullOrWhiteSpace(CommitMessage))
            {
                throw new InvalidOperationException(
                    _localizer.GetString("Error_CommitMessageRequired"));
            }

            await _gitService.CommitAsync(RepositoryPath, CommitMessage.Trim());
            CommitMessage = string.Empty;
            await RefreshCoreAsync();
            StatusMessage = _localizer.GetString("Status_CommitCreated");
        },
        "Status_Committing");

    private Task FetchAsync() => RunBusyAsync(
        async () =>
        {
            await _gitService.FetchAsync(RepositoryPath);
            await RefreshCoreAsync();
            StatusMessage = _localizer.GetString("Status_FetchComplete");
        },
        "Status_Fetching");

    private Task PullAsync() => RunBusyAsync(
        async () =>
        {
            await _gitService.PullAsync(RepositoryPath);
            await RefreshCoreAsync();
            StatusMessage = _localizer.GetString("Status_PullComplete");
        },
        "Status_Pulling");

    private Task PushAsync() => RunBusyAsync(
        async () =>
        {
            await _gitService.PushAsync(RepositoryPath);
            await RefreshCoreAsync();
            StatusMessage = _localizer.GetString("Status_PushComplete");
        },
        "Status_Pushing");

    private Task SwitchBranchAsync() => RunBusyAsync(
        async () =>
        {
            GitBranch branch = SelectedBranch
                ?? throw new InvalidOperationException(_localizer.GetString("Error_SelectBranch"));
            await _gitService.SwitchBranchAsync(RepositoryPath, branch.Name);
            await RefreshCoreAsync();
            StatusMessage = _localizer.Format("Status_BranchSwitched", branch.Name);
        },
        "Status_SwitchingBranch");

    private Task CreateBranchAsync() => RunBusyAsync(
        async () =>
        {
            string branchName = NewBranchName.Trim();
            await _gitService.CreateBranchAsync(RepositoryPath, branchName);
            NewBranchName = string.Empty;
            await RefreshCoreAsync();
            StatusMessage = _localizer.Format("Status_BranchCreated", branchName);
        },
        "Status_CreatingBranch");

    private Task RenameBranchAsync() => RunBusyAsync(
        async () =>
        {
            GitBranch branch = SelectedBranch
                ?? throw new InvalidOperationException(_localizer.GetString("Error_SelectBranch"));
            string newName = RenamedBranchName.Trim();
            await _gitService.RenameBranchAsync(RepositoryPath, branch.Name, newName);
            await RefreshCoreAsync();
            StatusMessage = _localizer.Format("Status_BranchRenamed", newName);
        },
        "Status_RenamingBranch");

    private Task DeleteBranchAsync() => RunBusyAsync(
        async () =>
        {
            GitBranch branch = SelectedBranch
                ?? throw new InvalidOperationException(_localizer.GetString("Error_SelectBranch"));
            await _gitService.DeleteBranchAsync(RepositoryPath, branch.Name);
            await RefreshCoreAsync();
            StatusMessage = _localizer.Format("Status_BranchDeleted", branch.Name);
        },
        "Status_DeletingBranch");

    private async Task RunBusyAsync(Func<Task> operation, string statusKey, params object[] arguments)
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        StatusMessage = arguments.Length == 0
            ? _localizer.GetString(statusKey)
            : _localizer.Format(statusKey, arguments);

        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            ShowUnexpectedError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUseRepository() => HasRepository && !IsBusy;

    private void ClearDiff()
    {
        DiffLines = [];
        _selectedChange = null;
        OnPropertyChanged(nameof(CanQuickEdit));
        NotifyDiffState();
    }

    private void NotifyDiffState()
    {
        OnPropertyChanged(nameof(HasDiff));
        OnPropertyChanged(nameof(HasNoDiff));
    }

    private bool CanUseFile(object? parameter) =>
        CanUseRepository() && parameter is GitFileChange;

    private bool CanToggleAllChanges() =>
        CanUseRepository() &&
        !HasConflicts &&
        (UnstagedFiles.Count > 0 || StagedFiles.Count > 0);

    private bool CanCommit() =>
        CanUseRepository() &&
        !HasConflicts &&
        StagedFiles.Count > 0 &&
        !string.IsNullOrWhiteSpace(CommitMessage);

    private bool CanSwitchBranch() =>
        CanUseRepository() && !HasConflicts && SelectedBranch is { IsCurrent: false };

    private bool CanCreateBranch() =>
        CanUseRepository() && !HasConflicts && !string.IsNullOrWhiteSpace(NewBranchName);

    private bool CanRenameBranch() =>
        CanUseRepository() &&
        !HasConflicts &&
        SelectedBranch is not null &&
        !string.IsNullOrWhiteSpace(RenamedBranchName) &&
        !string.Equals(SelectedBranch.Name, RenamedBranchName.Trim(), StringComparison.Ordinal);

    private bool CanDeleteBranch() =>
        CanUseRepository() && !HasConflicts && SelectedBranch is { IsCurrent: false };

    private void ShowUnexpectedError(Exception exception)
    {
        ErrorMessage = exception.Message;
        StatusMessage = _localizer.GetString("Status_OperationFailed");
    }

    private void NotifyCommands()
    {
        _openRepositoryCommand.RaiseCanExecuteChanged();
        _initializeRepositoryCommand.RaiseCanExecuteChanged();
        _refreshCommand.RaiseCanExecuteChanged();
        _stageCommand.RaiseCanExecuteChanged();
        _unstageCommand.RaiseCanExecuteChanged();
        _toggleAllChangesCommand.RaiseCanExecuteChanged();
        _commitCommand.RaiseCanExecuteChanged();
        _fetchCommand.RaiseCanExecuteChanged();
        _pullCommand.RaiseCanExecuteChanged();
        _pushCommand.RaiseCanExecuteChanged();
        NotifyBranchCommands();
    }

    private bool CanInitializeRepository() =>
        HasRepositorySuggestion && !IsBusy;

    private void NotifyBranchCommands()
    {
        OnPropertyChanged(nameof(CanMergeSelectedBranch));
        _switchBranchCommand.RaiseCanExecuteChanged();
        _createBranchCommand.RaiseCanExecuteChanged();
        _renameBranchCommand.RaiseCanExecuteChanged();
        _deleteBranchCommand.RaiseCanExecuteChanged();
    }
}
