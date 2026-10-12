using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Represents one selectable working-tree path in repository options.</summary>
public sealed class RepositoryFileItem : ViewModelBase
{
    private readonly ILocalizationService _localizer;
    private bool _isIncluded;

    /// <summary>Initializes a selectable file from its Git state.</summary>
    public RepositoryFileItem(GitRepositoryFile file, ILocalizationService localizer)
    {
        _localizer = localizer;
        Path = file.Path;
        IsTracked = file.IsTracked;
        _isIncluded = file.IsTracked || !file.IsIgnored;
    }

    /// <summary>Gets the repository-relative path.</summary>
    public string Path { get; }

    /// <summary>Gets whether the path is already stored in the Git index.</summary>
    public bool IsTracked { get; }

    /// <summary>Gets whether the checkbox may change the path state.</summary>
    public bool CanChange => !IsTracked;

    /// <summary>Gets or sets whether the managed .gitignore should include the path.</summary>
    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (!IsTracked && SetProperty(ref _isIncluded, value))
            {
                OnPropertyChanged(nameof(StatusLabel));
            }
        }
    }

    /// <summary>Gets a localized description of the effective Git state.</summary>
    public string StatusLabel => _localizer.GetString(
        IsTracked
            ? "RepositoryFile_Tracked"
            : IsIncluded
                ? "RepositoryFile_Included"
                : "RepositoryFile_Ignored");
}
