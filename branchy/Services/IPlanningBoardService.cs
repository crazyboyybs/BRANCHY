using Branchy.Models;

namespace Branchy.Services;

/// <summary>Loads and saves repository-specific planning boards.</summary>
public interface IPlanningBoardService
{
    /// <summary>Loads the board associated with a repository path.</summary>
    Task<PlanningBoard> LoadAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the board associated with a repository path.</summary>
    Task SaveAsync(
        string repositoryPath,
        PlanningBoard board,
        CancellationToken cancellationToken = default);
}
