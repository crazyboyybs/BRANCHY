namespace Branchy.Models;

/// <summary>Describes a Git operation that may stop with unresolved conflicts.</summary>
public sealed record GitOperationResult(
    bool IsCompleted,
    IReadOnlyList<string> ConflictedPaths,
    string Output)
{
    /// <summary>Gets whether the operation stopped in a resolvable conflict state.</summary>
    public bool HasConflicts => ConflictedPaths.Count > 0;
}
