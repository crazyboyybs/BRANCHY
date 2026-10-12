namespace Branchy.Models;

/// <summary>Identifies the commit data considered by a history search.</summary>
public enum HistorySearchScope
{
    All,
    Message,
    Diff,
    Branch,
}
