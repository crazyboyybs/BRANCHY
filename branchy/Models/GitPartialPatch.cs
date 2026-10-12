namespace Branchy.Models;

/// <summary>Contains the file header and selectable hunks of an unstaged patch.</summary>
public sealed record GitPartialPatch(
    string FileHeader,
    IReadOnlyList<GitPatchHunk> Hunks);
