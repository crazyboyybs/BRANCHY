using System.Collections.ObjectModel;
using Branchy.Models;

namespace Branchy.ViewModels;

/// <summary>Wraps one selectable patch hunk for the partial-stage dialog.</summary>
public sealed class PartialStageHunkItem : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    /// <summary>Initializes a selectable hunk.</summary>
    public PartialStageHunkItem(GitPatchHunk hunk, Action selectionChanged)
    {
        Hunk = hunk;
        _selectionChanged = selectionChanged;
        Lines = new ObservableCollection<GitDiffLine>(
            hunk.Lines.Where(line => line.Kind != GitDiffLineKind.HunkHeader));
    }

    /// <summary>Gets the underlying patch hunk.</summary>
    public GitPatchHunk Hunk { get; }

    /// <summary>Gets the hunk location displayed beside its checkbox.</summary>
    public string Summary => Hunk.Header;

    /// <summary>Gets the semantic lines displayed in the hunk preview.</summary>
    public ObservableCollection<GitDiffLine> Lines { get; }

    /// <summary>Gets or sets whether this hunk will be staged.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged();
            }
        }
    }
}
