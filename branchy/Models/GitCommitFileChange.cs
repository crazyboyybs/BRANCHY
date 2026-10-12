namespace Branchy.Models;

/// <summary>Represents one path changed by a commit.</summary>
public sealed record GitCommitFileChange(
    string Status,
    string Path,
    string? OriginalPath)
{
    /// <summary>Gets the compact status code reported by Git.</summary>
    public string StatusCode => string.IsNullOrEmpty(Status) ? "?" : Status[..1];

    /// <summary>Gets a display path that preserves rename and copy origins.</summary>
    public string DisplayPath => string.IsNullOrWhiteSpace(OriginalPath)
        ? Path
        : $"{OriginalPath} → {Path}";
}
