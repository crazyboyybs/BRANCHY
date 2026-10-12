using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates commit selection for an amend operation.</summary>
public sealed class AmendCommitViewModel : ViewModelBase
{
    private readonly string _repositoryPath;
    private readonly IGitService _gitService;
    private readonly ILocalizationService _localizer;
    private GitCommit? _selectedCommit;
    private string _commitMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading = true;

    /// <summary>Initializes commit selection for the active repository.</summary>
    public AmendCommitViewModel(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _localizer = localizer;
    }

    /// <summary>Gets the recent commits in the current history.</summary>
    public ObservableCollection<GitCommit> Commits { get; } = [];

    /// <summary>Gets or sets the commit that will receive the staged changes.</summary>
    public GitCommit? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            if (SetProperty(ref _selectedCommit, value))
            {
                CommitMessage = value?.Message ?? string.Empty;
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    /// <summary>Gets or sets the complete message of the rewritten commit.</summary>
    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (SetProperty(ref _commitMessage, value))
            {
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    /// <summary>Gets the error shown when commit history cannot be loaded.</summary>
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

    /// <summary>Gets whether commit history is being loaded.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    /// <summary>Gets whether the dialog has an actionable loading error.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Gets whether the selected commit can be confirmed.</summary>
    public bool CanConfirm =>
        SelectedCommit is not null &&
        !string.IsNullOrWhiteSpace(CommitMessage) &&
        !IsLoading;

    /// <summary>Loads recent commits and preselects the current HEAD.</summary>
    public async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        IsLoading = true;
        try
        {
            IReadOnlyList<GitCommit> commits = await _gitService.GetCommitsAsync(_repositoryPath);
            Commits.Clear();
            foreach (GitCommit commit in commits)
            {
                Commits.Add(commit);
            }

            SelectedCommit = Commits.FirstOrDefault();
            if (SelectedCommit is null)
            {
                ErrorMessage = _localizer.GetString("Error_NoCommitsForAmend");
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
