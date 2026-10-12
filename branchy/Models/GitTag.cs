namespace Branchy.Models;

/// <summary>Describes a lightweight or annotated Git tag.</summary>
public sealed record GitTag(
    string Name,
    string TargetHash,
    string Subject,
    bool IsAnnotated)
{
    /// <summary>Gets the abbreviated target object identifier.</summary>
    public string ShortTargetHash => TargetHash.Length > 8 ? TargetHash[..8] : TargetHash;

    /// <summary>Gets whether a target subject is available.</summary>
    public bool HasSubject => !string.IsNullOrWhiteSpace(Subject);
}
