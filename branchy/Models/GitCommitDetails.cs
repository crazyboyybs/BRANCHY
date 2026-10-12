namespace Branchy.Models;

/// <summary>Contains the paths and textual patch introduced by a commit.</summary>
public sealed record GitCommitDetails(
    IReadOnlyList<GitCommitFileChange> Files,
    string Diff);
