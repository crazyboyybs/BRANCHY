namespace Branchy.Models;

/// <summary>Contains the metadata needed to display one commit in the visual history.</summary>
public sealed record GitHistoryEntry(
    string Hash,
    string ShortHash,
    IReadOnlyList<string> ParentHashes,
    string References,
    string Author,
    string AuthorEmail,
    DateTimeOffset AuthoredAt,
    string Subject,
    string Message)
{
    /// <summary>Gets a compact, locale-aware author and date summary.</summary>
    public string AuthorAndDate => $"{Author} · {AuthoredAt:d}";

    /// <summary>Gets the author name and email address.</summary>
    public string AuthorIdentity => string.IsNullOrWhiteSpace(AuthorEmail)
        ? Author
        : $"{Author} <{AuthorEmail}>";

    /// <summary>Gets whether the commit has branch, tag, or HEAD decorations.</summary>
    public bool HasReferences => !string.IsNullOrWhiteSpace(References);

    /// <summary>Gets whether the commit joins more than one parent.</summary>
    public bool IsMerge => ParentHashes.Count > 1;
}
