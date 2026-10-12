using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates local tag creation, inspection, and deletion.</summary>
public sealed class TagManagerViewModel : ViewModelBase
{
    private readonly string _repositoryPath;
    private readonly IGitService _gitService;
    private readonly ILocalizationService _localizer;
    private GitTag? _selectedTag;
    private string _newTagName = string.Empty;
    private string _target = "HEAD";
    private string _annotationMessage = string.Empty;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    /// <summary>Initializes tag management for the active repository.</summary>
    public TagManagerViewModel(
        string repositoryPath,
        IGitService gitService,
        ILocalizationService localizer)
    {
        _repositoryPath = repositoryPath;
        _gitService = gitService;
        _localizer = localizer;
        _statusMessage = localizer.GetString("Tags_Loading");
    }

    /// <summary>Gets the local tags.</summary>
    public ObservableCollection<GitTag> Tags { get; } = [];

    /// <summary>Gets or sets the selected tag.</summary>
    public GitTag? SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value))
            {
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    /// <summary>Gets or sets the name of the tag to create.</summary>
    public string NewTagName
    {
        get => _newTagName;
        set
        {
            if (SetProperty(ref _newTagName, value))
            {
                OnPropertyChanged(nameof(CanCreate));
            }
        }
    }

    /// <summary>Gets or sets the commit or reference targeted by a new tag.</summary>
    public string Target
    {
        get => _target;
        set
        {
            if (SetProperty(ref _target, value))
            {
                OnPropertyChanged(nameof(CanCreate));
            }
        }
    }

    /// <summary>Gets or sets the optional annotated-tag message.</summary>
    public string AnnotationMessage
    {
        get => _annotationMessage;
        set => SetProperty(ref _annotationMessage, value);
    }

    /// <summary>Gets the operation status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets the actionable error message.</summary>
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

    /// <summary>Gets whether an operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanCreate));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    /// <summary>Gets whether an error is available.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Gets whether the tag list is empty.</summary>
    public bool HasNoTags => Tags.Count == 0 && !IsBusy;

    /// <summary>Gets whether a new tag can be created.</summary>
    public bool CanCreate =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(NewTagName) &&
        !string.IsNullOrWhiteSpace(Target);

    /// <summary>Gets whether the selected tag can be deleted.</summary>
    public bool CanDelete => !IsBusy && SelectedTag is not null;

    /// <summary>Loads the local tags.</summary>
    public Task LoadAsync() => RunAsync(
        async () =>
        {
            await ReloadAsync();
            StatusMessage = Tags.Count == 0
                ? _localizer.GetString("Tags_Empty")
                : _localizer.Format("Tags_Loaded", Tags.Count);
        },
        "Tags_Loading");

    /// <summary>Creates the requested local tag.</summary>
    public Task CreateAsync()
    {
        if (!CanCreate)
        {
            return Task.CompletedTask;
        }

        return RunAsync(
            async () =>
            {
                string name = NewTagName.Trim();
                await _gitService.CreateTagAsync(
                    _repositoryPath,
                    name,
                    Target,
                    AnnotationMessage);
                NewTagName = string.Empty;
                AnnotationMessage = string.Empty;
                await ReloadAsync();
                SelectedTag = Tags.FirstOrDefault(tag => tag.Name == name);
                StatusMessage = _localizer.Format("Tags_Created", name);
            },
            "Tags_Creating");
    }

    /// <summary>Deletes the selected local tag.</summary>
    public Task DeleteSelectedAsync()
    {
        GitTag? tag = SelectedTag;
        if (!CanDelete || tag is null)
        {
            return Task.CompletedTask;
        }

        return RunAsync(
            async () =>
            {
                await _gitService.DeleteTagAsync(_repositoryPath, tag.Name);
                await ReloadAsync();
                StatusMessage = _localizer.Format("Tags_Deleted", tag.Name);
            },
            "Tags_Deleting");
    }

    private async Task ReloadAsync()
    {
        string? selectedName = SelectedTag?.Name;
        IReadOnlyList<GitTag> tags = await _gitService.GetTagsAsync(_repositoryPath);
        Tags.Clear();
        foreach (GitTag tag in tags)
        {
            Tags.Add(tag);
        }

        SelectedTag = Tags.FirstOrDefault(tag => tag.Name == selectedName)
            ?? Tags.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoTags));
    }

    private async Task RunAsync(Func<Task> operation, string statusKey)
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        StatusMessage = _localizer.GetString(statusKey);
        IsBusy = true;
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
            OnPropertyChanged(nameof(HasNoTags));
        }
    }
}
