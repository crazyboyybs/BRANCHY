namespace Branchy.Models;

/// <summary>Describes a local branch and its configured upstream.</summary>
public sealed record GitBranch(string Name, bool IsCurrent, string? Upstream)
{
    /// <summary>Gets whether the branch has an upstream configured.</summary>
    public bool HasUpstream => !string.IsNullOrWhiteSpace(Upstream);
}
