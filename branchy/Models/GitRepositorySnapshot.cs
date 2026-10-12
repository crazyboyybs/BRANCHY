namespace Branchy.Models;

/// <summary>Contains the repository state required by the main workspace.</summary>
public sealed record GitRepositorySnapshot(
    string RootPath,
    string RepositoryName,
    string BranchName,
    string HeadOid,
    string? Upstream,
    int Ahead,
    int Behind,
    IReadOnlyList<GitFileChange> Changes);
