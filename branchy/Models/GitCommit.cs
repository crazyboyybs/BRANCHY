namespace Branchy.Models;

/// <summary>Contains the commit metadata displayed by the archive exporter.</summary>
public sealed record GitCommit(
    string Hash,
    string ShortHash,
    string Author,
    DateTimeOffset AuthoredAt,
    string Subject,
    string Message)
{
    /// <summary>Gets a compact, locale-aware author and date summary.</summary>
    public string AuthorAndDate => $"{Author} · {AuthoredAt:g}";
}
