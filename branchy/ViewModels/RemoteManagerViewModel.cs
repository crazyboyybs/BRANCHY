using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates creation, editing, fetching, and removal of Git remotes.</summary>
public sealed class RemoteManagerViewModel : ViewModelBase
{
    private readonly string _repositoryPath;
    private readonly IGitService _gitService;
    private readonly ILocalizationService _localizer;
    private GitRemote? _selectedRemote;
    private string _remoteName = string.Empty;
    private string _fetchUrl = string.Empty;
    private string _pushUrl = string.Empty;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private bool _isCreating;
    private bool _isConfirmingDelete;
    private bool _isBusy;

    /// <summary>Initializes remote management for the active repository.</summary>
    public RemoteManagerViewModel(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _localizer = localizer;
        _statusMessage = localizer.GetString("Remotes_Loading");
    }

    /// <summary>Gets the configured remotes.</summary>
    public ObservableCollection<GitRemote> Remotes { get; } = [];

    /// <summary>Gets or sets the remote selected for editing.</summary>
    public GitRemote? SelectedRemote
    {
        get => _selectedRemote;
        set
        {
            if (!SetProperty(ref _selectedRemote, value))
            {
                return;
            }

            IsConfirmingDelete = false;
            if (value is not null)
            {
                IsCreating = false;
                RemoteName = value.Name;
                FetchUrl = value.FetchUrl;
                PushUrl = value.PushUrl ?? string.Empty;
            }

            NotifyActionState();
        }
    }

    /// <summary>Gets or sets the name being edited.</summary>
    public string RemoteName
    {
        get => _remoteName;
        set
        {
            if (SetProperty(ref _remoteName, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>Gets or sets the fetch URL being edited.</summary>
    public string FetchUrl
    {
        get => _fetchUrl;
        set
        {
            if (SetProperty(ref _fetchUrl, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>Gets or sets the optional separate push URL.</summary>
    public string PushUrl
    {
        get => _pushUrl;
        set => SetProperty(ref _pushUrl, value);
    }

    /// <summary>Gets whether the editor is creating a new remote.</summary>
    public bool IsCreating
    {
        get => _isCreating;
        private set
        {
            if (SetProperty(ref _isCreating, value))
            {
                OnPropertyChanged(nameof(FormTitle));
                NotifyActionState();
            }
        }
    }

    /// <summary>Gets the localized editor heading.</summary>
    public string FormTitle => _localizer.GetString(
        IsCreating ? "Remotes_NewRemoteTitle" : "Remotes_SelectedRemoteTitle");

    /// <summary>Gets whether no remotes are configured.</summary>
    public bool HasNoRemotes => Remotes.Count == 0;

    /// <summary>Gets whether the current editor values can be saved.</summary>
    public bool CanSave =>
        !IsBusy &&
        (IsCreating || SelectedRemote is not null) &&
        !string.IsNullOrWhiteSpace(RemoteName) &&
        !string.IsNullOrWhiteSpace(FetchUrl);

    /// <summary>Gets whether the selected remote can be fetched.</summary>
    public bool CanFetch => !IsBusy && !IsCreating && SelectedRemote is not null;

    /// <summary>Gets whether removal confirmation can be requested.</summary>
    public bool CanDelete => !IsBusy && !IsCreating && SelectedRemote is not null;

    /// <summary>Gets whether the inline removal confirmation is visible.</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set => SetProperty(ref _isConfirmingDelete, value);
    }

    /// <summary>Gets the localized remote removal warning.</summary>
    public string DeleteConfirmationMessage => SelectedRemote is null
        ? string.Empty
        : _localizer.Format("Remotes_DeleteConfirmation", SelectedRemote.Name);

    /// <summary>Gets the current remote operation status.</summary>
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

    /// <summary>Gets whether a remote operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyActionState();
            }
        }
    }

    /// <summary>Loads configured remotes.</summary>
    public Task LoadAsync() => ReloadAsync(null, "Remotes_Loading");

    /// <summary>Clears the editor for a new remote.</summary>
    public void StartCreate()
    {
        SelectedRemote = null;
        IsCreating = true;
        IsConfirmingDelete = false;
        RemoteName = string.Empty;
        FetchUrl = string.Empty;
        PushUrl = string.Empty;
        ErrorMessage = string.Empty;
        StatusMessage = _localizer.GetString("Remotes_EnterDetails");
    }

    /// <summary>Saves a new remote or updates the selected one.</summary>
    public Task SaveAsync()
    {
        if (!CanSave)
        {
            return Task.CompletedTask;
        }

        string name = RemoteName.Trim();
        string fetchUrl = FetchUrl.Trim();
        string? pushUrl = string.IsNullOrWhiteSpace(PushUrl) ? null : PushUrl.Trim();
        GitRemote? existing = SelectedRemote;
        return RunBusyAsync(
            async () =>
            {
                if (IsCreating)
                {
                    await _gitService.AddRemoteAsync(
                        _repositoryPath,
                        name,
                        fetchUrl,
                        pushUrl);
                }
                else if (existing is not null)
                {
                    await _gitService.UpdateRemoteAsync(
                        _repositoryPath,
                        existing.Name,
                        name,
                        fetchUrl,
                        pushUrl);
                }

                await LoadRemotesCoreAsync(name);
                StatusMessage = _localizer.Format("Remotes_Saved", name);
            },
            IsCreating ? "Remotes_Adding" : "Remotes_Saving");
    }

    /// <summary>Fetches and prunes the selected remote.</summary>
    public Task FetchSelectedAsync()
    {
        GitRemote? remote = SelectedRemote;
        if (!CanFetch || remote is null)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                await _gitService.FetchRemoteAsync(_repositoryPath, remote.Name);
                StatusMessage = _localizer.Format("Remotes_Fetched", remote.Name);
            },
            "Remotes_Fetching",
            remote.Name);
    }

    /// <summary>Shows the inline removal confirmation.</summary>
    public void RequestDelete()
    {
        if (CanDelete)
        {
            IsConfirmingDelete = true;
            OnPropertyChanged(nameof(DeleteConfirmationMessage));
        }
    }

    /// <summary>Closes the inline removal confirmation.</summary>
    public void CancelDelete() => IsConfirmingDelete = false;

    /// <summary>Removes the selected remote after confirmation.</summary>
    public Task ConfirmDeleteAsync()
    {
        GitRemote? remote = SelectedRemote;
        if (!CanDelete || remote is null || !IsConfirmingDelete)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(
            async () =>
            {
                await _gitService.RemoveRemoteAsync(_repositoryPath, remote.Name);
                await LoadRemotesCoreAsync(null);
                StatusMessage = _localizer.Format("Remotes_Removed", remote.Name);
            },
            "Remotes_Removing",
            remote.Name);
    }

    private Task ReloadAsync(string? selectedName, string statusKey) => RunBusyAsync(
        async () =>
        {
            await LoadRemotesCoreAsync(selectedName);
            StatusMessage = Remotes.Count == 0
                ? _localizer.GetString("Remotes_Empty")
                : _localizer.Format("Remotes_Loaded", Remotes.Count);
        },
        statusKey);

    private async Task LoadRemotesCoreAsync(string? selectedName)
    {
        IReadOnlyList<GitRemote> remotes = await _gitService.GetRemotesAsync(_repositoryPath);
        Remotes.Clear();
        foreach (GitRemote remote in remotes)
        {
            Remotes.Add(remote);
        }

        OnPropertyChanged(nameof(HasNoRemotes));
        IsCreating = Remotes.Count == 0;
        SelectedRemote = Remotes.FirstOrDefault(remote => remote.Name == selectedName)
            ?? Remotes.FirstOrDefault();
        if (SelectedRemote is null)
        {
            StartCreate();
        }
    }

    private async Task RunBusyAsync(
        Func<Task> operation,
        string statusKey,
        params object[] arguments)
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
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("Status_OperationFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyActionState()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanFetch));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(DeleteConfirmationMessage));
    }
}
