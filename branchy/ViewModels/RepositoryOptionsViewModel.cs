using System.Collections.ObjectModel;
using System.ComponentModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates the interactive .gitignore selection.</summary>
public sealed class RepositoryOptionsViewModel : ViewModelBase
{
    private readonly string _repositoryPath;
    private readonly IGitService _gitService;
    private readonly ILocalizationService _localizer;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    /// <summary>Initializes repository-specific operations.</summary>
    public RepositoryOptionsViewModel(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _localizer = localizer;
        _statusMessage = localizer.GetString("RepositoryOptions_Loading");
    }

    /// <summary>Gets every path visible to the interactive .gitignore editor.</summary>
    public ObservableCollection<RepositoryFileItem> Files { get; } = [];

    /// <summary>Gets a localized summary of the current file selection.</summary>
    public string SelectionSummary => _localizer.Format(
        "RepositoryFiles_SelectionSummary",
        Files.Count(file => file.IsIncluded),
        Files.Count(file => !file.IsIncluded),
        Files.Count(file => file.IsTracked));

    /// <summary>Gets the current repository operation status.</summary>
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

    /// <summary>Gets whether the repository options error bar is visible.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Gets whether an operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanSaveIgnore));
            }
        }
    }

    /// <summary>Gets whether the managed .gitignore can be saved.</summary>
    public bool CanSaveIgnore => Files.Count > 0 && !IsBusy;

    /// <summary>Loads repository files from Git.</summary>
    public Task LoadAsync() => RunBusyAsync(
        async () =>
        {
            await LoadFilesCoreAsync();
            StatusMessage = _localizer.GetString("RepositoryOptions_Ready");
        },
        "RepositoryOptions_Loading");

    /// <summary>Selects every untracked path for inclusion.</summary>
    public void SelectAll()
    {
        foreach (RepositoryFileItem file in Files.Where(file => file.CanChange))
        {
            file.IsIncluded = true;
        }
    }

    /// <summary>Leaves tracked files selected and ignores every other path.</summary>
    public void ClearOptionalSelection()
    {
        foreach (RepositoryFileItem file in Files.Where(file => file.CanChange))
        {
            file.IsIncluded = false;
        }
    }

    /// <summary>Writes the managed root .gitignore section.</summary>
    public Task SaveGitIgnoreAsync() => RunBusyAsync(
        async () =>
        {
            string[] includedPaths = Files
                .Where(file => file.IsIncluded)
                .Select(file => file.Path)
                .ToArray();
            await _gitService.UpdateManagedGitIgnoreAsync(_repositoryPath, includedPaths);
            await LoadFilesCoreAsync();
            StatusMessage = _localizer.GetString("RepositoryOptions_IgnoreSaved");
        },
        "RepositoryOptions_SavingIgnore");

    private async Task LoadFilesCoreAsync()
    {
        foreach (RepositoryFileItem oldItem in Files)
        {
            oldItem.PropertyChanged -= File_PropertyChanged;
        }

        IReadOnlyList<GitRepositoryFile> files = await _gitService.GetRepositoryFilesAsync(_repositoryPath);
        Files.Clear();
        foreach (GitRepositoryFile file in files)
        {
            var item = new RepositoryFileItem(file, _localizer);
            item.PropertyChanged += File_PropertyChanged;
            Files.Add(item);
        }

        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanSaveIgnore));
    }

    private void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryFileItem.IsIncluded))
        {
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    private async Task RunBusyAsync(Func<Task> operation, string statusKey)
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        StatusMessage = _localizer.GetString(statusKey);
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

}
