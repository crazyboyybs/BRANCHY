namespace Branchy.Models;

/// <summary>Describes how a working-tree file currently relates to Git.</summary>
public sealed record GitRepositoryFile(
    string Path,
    bool IsTracked,
    bool IsIgnored);
