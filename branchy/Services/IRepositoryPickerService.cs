namespace Branchy.Services;

/// <summary>Abstracts the system folder picker from the ViewModel.</summary>
public interface IRepositoryPickerService
{
    /// <summary>Lets the user choose a repository folder.</summary>
    Task<string?> PickRepositoryAsync();
}
