namespace Branchy.Models;

/// <summary>Contains one independently selectable unified-diff hunk.</summary>
public sealed record GitPatchHunk(
    string Header,
    string PatchText,
    IReadOnlyList<GitDiffLine> Lines);
