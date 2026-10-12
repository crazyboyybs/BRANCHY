namespace Branchy.Services;

/// <summary>Abstracts the system save picker used by commit archives.</summary>
public interface IArchivePickerService
{
    /// <summary>Lets the user choose a destination ZIP path.</summary>
    Task<string?> PickArchivePathAsync(string suggestedFileName);
}
