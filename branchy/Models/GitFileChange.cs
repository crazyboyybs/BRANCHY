namespace Branchy.Models;

/// <summary>Describes one path reported by Git porcelain status.</summary>
public sealed record GitFileChange(
    string Path,
    string? OriginalPath,
    char IndexStatus,
    char WorkTreeStatus)
{
    /// <summary>Gets whether Git reports an unresolved merge state for this path.</summary>
    public bool IsConflicted =>
        IndexStatus == 'U' ||
        WorkTreeStatus == 'U' ||
        (IndexStatus == 'A' && WorkTreeStatus == 'A') ||
        (IndexStatus == 'D' && WorkTreeStatus == 'D');

    /// <summary>Gets whether the index contains a change for this path.</summary>
    public bool IsStaged => !IsConflicted && IndexStatus is not ('.' or '?' or '!');

    /// <summary>Gets whether the working tree contains a change for this path.</summary>
    public bool HasWorkingTreeChange => IsConflicted || WorkTreeStatus != '.' || IsUntracked;

    /// <summary>Gets whether Git reports this path as untracked.</summary>
    public bool IsUntracked => IndexStatus == '?' && WorkTreeStatus == '?';

    /// <summary>Gets whether a tracked file was deleted from the working tree.</summary>
    public bool IsWorkTreeDeletion => !IsUntracked && WorkTreeStatus == 'D';

    /// <summary>Gets whether this change can be restored or discarded from the working tree.</summary>
    public bool CanDiscardWorkingTreeChange => !IsConflicted && !IsUntracked && HasWorkingTreeChange;

    /// <summary>Gets whether the working-tree diff can be staged hunk by hunk.</summary>
    public bool CanPartiallyStage => !IsConflicted && !IsUntracked && WorkTreeStatus == 'M';

    /// <summary>Gets a compact status code for the index.</summary>
    public string IndexStatusCode => IndexStatus.ToString();

    /// <summary>Gets a compact status code for the working tree.</summary>
    public string WorkTreeStatusCode => IsConflicted ? "!" : IsUntracked ? "?" : WorkTreeStatus.ToString();
}
