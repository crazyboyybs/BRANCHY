namespace Branchy.Models;

/// <summary>Identifies the kind of work represented by a planning item.</summary>
public enum PlanningItemType
{
    Feature,
    Bug,
    Implementation,
    Improvement,
}

/// <summary>Identifies the workflow column that owns a planning item.</summary>
public enum PlanningItemStatus
{
    Ideas,
    Planned,
    InProgress,
    Completed,
}

/// <summary>Stores one implementation, feature, bug, or improvement in a planning board.</summary>
public sealed class PlanningItem
{
    /// <summary>Gets or sets the stable item identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the concise item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the short item description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the detailed implementation documentation.</summary>
    public string Documentation { get; set; } = string.Empty;

    /// <summary>Gets or sets the item kind.</summary>
    public PlanningItemType Type { get; set; }

    /// <summary>Gets or sets the workflow column.</summary>
    public PlanningItemStatus Status { get; set; }

    /// <summary>Gets or sets completion progress from zero to one hundred.</summary>
    public int Progress { get; set; }

    /// <summary>Gets or sets the user-created tags attached to the item.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Gets or sets the full hash of an associated commit.</summary>
    public string LinkedCommitHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the short hash of an associated commit.</summary>
    public string LinkedCommitShortHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the subject of an associated commit.</summary>
    public string LinkedCommitSubject { get; set; } = string.Empty;

    /// <summary>Gets or sets when the item was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the item was most recently edited.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Stores the versioned planning data for one repository.</summary>
public sealed class PlanningBoard
{
    /// <summary>Gets or sets the persisted document schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the board items.</summary>
    public List<PlanningItem> Items { get; set; } = [];
}
