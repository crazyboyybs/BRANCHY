using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Coordinates repository clone input, validation, and execution.</summary>
public sealed class CloneRepositoryViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly IRepositoryPickerService _repositoryPicker;
    private readonly ILocalizationService _localizer;
    private string _repositorySource = string.Empty;
    private string _destinationParent = string.Empty;
    private string _directoryName = string.Empty;
    private string _suggestedDirectoryName = string.Empty;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    /// <summary>Initializes a clone workflow using platform-independent services.</summary>
    public CloneRepositoryViewModel(
        IGitService gitService,
        IRepositoryPickerService repositoryPicker,
        ILocalizationService localizer)
    {
        _gitService = gitService;
        _repositoryPicker = repositoryPicker;
        _localizer = localizer;
        _statusMessage = localizer.GetString("Clone_Ready");
    }

    /// <summary>Gets or sets the repository URL or local source path.</summary>
    public string RepositorySource
    {
        get => _repositorySource;
        set
        {
            if (!SetProperty(ref _repositorySource, value))
            {
                return;
            }

            string suggestion = InferDirectoryName(value);
            if (string.IsNullOrWhiteSpace(DirectoryName) ||
                string.Equals(DirectoryName, _suggestedDirectoryName, StringComparison.Ordinal))
            {
                _directoryName = suggestion;
                OnPropertyChanged(nameof(DirectoryName));
                OnPropertyChanged(nameof(DestinationPreview));
            }

            _suggestedDirectoryName = suggestion;
            NotifyAvailability();
        }
    }

    /// <summary>Gets or sets the existing folder that will contain the clone.</summary>
    public string DestinationParent
    {
        get => _destinationParent;
        set
        {
            if (SetProperty(ref _destinationParent, value))
            {
                OnPropertyChanged(nameof(DestinationPreview));
                NotifyAvailability();
            }
        }
    }

    /// <summary>Gets or sets the new directory name.</summary>
    public string DirectoryName
    {
        get => _directoryName;
        set
        {
            if (SetProperty(ref _directoryName, value))
            {
                OnPropertyChanged(nameof(DestinationPreview));
                NotifyAvailability();
            }
        }
    }

    /// <summary>Gets the complete destination shown before cloning.</summary>
    public string DestinationPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DestinationParent))
            {
                return DirectoryName.Trim();
            }

            return string.IsNullOrWhiteSpace(DirectoryName)
                ? DestinationParent.Trim()
                : Path.Combine(DestinationParent.Trim(), DirectoryName.Trim());
        }
    }

    /// <summary>Gets the current clone status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets the current validation or Git error.</summary>
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

    /// <summary>Gets whether Git is cloning the repository.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyAvailability();
            }
        }
    }

    /// <summary>Gets whether the current input can start a clone.</summary>
    public bool CanClone =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(RepositorySource) &&
        !string.IsNullOrWhiteSpace(DestinationParent) &&
        !string.IsNullOrWhiteSpace(DirectoryName);

    /// <summary>Gets whether the dialog can be dismissed.</summary>
    public bool CanCancel => !IsBusy;

    /// <summary>Gets the repository path returned by a successful clone.</summary>
    public string? ClonedRepositoryPath { get; private set; }

    /// <summary>Lets the user choose the parent folder for the clone.</summary>
    public async Task BrowseDestinationAsync()
    {
        if (IsBusy)
        {
            return;
        }

        string? path = await _repositoryPicker.PickRepositoryAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            DestinationParent = path;
        }
    }

    /// <summary>Validates the input and clones the repository.</summary>
    public async Task<bool> CloneAsync()
    {
        if (!CanClone)
        {
            ErrorMessage = _localizer.GetString("Error_CloneFieldsRequired");
            return false;
        }

        try
        {
            string source = NormalizeSource(RepositorySource);
            string destination = BuildDestinationPath();
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = _localizer.GetString("Clone_Cloning");
            ClonedRepositoryPath = await _gitService.CloneRepositoryAsync(source, destination);
            StatusMessage = _localizer.GetString("Clone_Complete");
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("Clone_Failed");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildDestinationPath()
    {
        string parent = Path.GetFullPath(DestinationParent.Trim());
        if (!Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException(
                _localizer.GetString("Error_CloneDestinationMissing"));
        }

        string directoryName = DirectoryName.Trim();
        if (directoryName is "." or ".." ||
            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            directoryName.Contains(Path.DirectorySeparatorChar) ||
            directoryName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException(
                _localizer.GetString("Error_CloneDirectoryNameInvalid"));
        }

        string destination = Path.GetFullPath(Path.Combine(parent, directoryName));
        string? actualParent = Path.GetDirectoryName(destination);
        if (!string.Equals(parent, actualParent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                _localizer.GetString("Error_CloneDirectoryNameInvalid"));
        }

        return destination;
    }

    private static string NormalizeSource(string source)
    {
        string trimmed = source.Trim();
        return trimmed.Length >= 2 &&
               trimmed[0] == '"' &&
               trimmed[^1] == '"'
            ? trimmed[1..^1]
            : trimmed;
    }

    private static string InferDirectoryName(string source)
    {
        string normalized = NormalizeSource(source)
            .Replace('\\', '/')
            .TrimEnd('/');
        int separator = Math.Max(normalized.LastIndexOf('/'), normalized.LastIndexOf(':'));
        string name = separator >= 0 ? normalized[(separator + 1)..] : normalized;
        return name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }

    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanClone));
        OnPropertyChanged(nameof(CanCancel));
    }
}
