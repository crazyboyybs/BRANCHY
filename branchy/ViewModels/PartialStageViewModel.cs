using System.Collections.ObjectModel;
using Branchy.Models;
using Branchy.Services;

namespace Branchy.ViewModels;

/// <summary>Loads and applies selected working-tree hunks to the Git index.</summary>
public sealed class PartialStageViewModel : ViewModelBase
{
    private readonly string _repositoryPath;
    private readonly string _path;
    private readonly IGitService _gitService;
    private readonly ILocalizationService _localizer;
    private GitPartialPatch? _patch;
    private string _statusMessage;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    /// <summary>Initializes partial staging for one repository path.</summary>
    public PartialStageViewModel(
        string repositoryPath,
        string path,
        IGitService gitService,
        ILocalizationService localizer)
    {
        _repositoryPath = repositoryPath;
        _path = path;
        _gitService = gitService;
        _localizer = localizer;
        _statusMessage = localizer.GetString("PartialStage_Loading");
    }

    /// <summary>Gets the path being partially staged.</summary>
    public string Path => _path;

    /// <summary>Gets selectable hunks in file order.</summary>
    public ObservableCollection<PartialStageHunkItem> Hunks { get; } = [];

    /// <summary>Gets whether any hunks are available.</summary>
    public bool HasHunks => Hunks.Count > 0;

    /// <summary>Gets whether no hunks are available.</summary>
    public bool HasNoHunks => !HasHunks && !IsBusy;

    /// <summary>Gets whether at least one selected hunk can be applied.</summary>
    public bool CanApply => !IsBusy && Hunks.Any(hunk => hunk.IsSelected);

    /// <summary>Gets a localized selection count.</summary>
    public string SelectionSummary => _localizer.Format(
        "PartialStage_SelectionSummary",
        Hunks.Count(hunk => hunk.IsSelected),
        Hunks.Count);

    /// <summary>Gets whether the selected hunks were applied.</summary>
    public bool WasApplied { get; private set; }

    /// <summary>Gets the current partial-stage status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets the current partial-stage error.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>Gets whether an error should be displayed.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Gets whether a partial-stage operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(HasNoHunks));
            }
        }
    }

    /// <summary>Loads selectable hunks from the current index-to-working-tree diff.</summary>
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        StatusMessage = _localizer.GetString("PartialStage_Loading");
        try
        {
            _patch = await _gitService.GetPartialStagePatchAsync(_repositoryPath, _path);
            Hunks.Clear();
            foreach (GitPatchHunk hunk in _patch.Hunks)
            {
                Hunks.Add(new PartialStageHunkItem(hunk, NotifySelectionChanged));
            }

            NotifyHunkState();
            StatusMessage = _localizer.Format("PartialStage_Loaded", Hunks.Count);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("PartialStage_Failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Selects every available hunk.</summary>
    public void SelectAll()
    {
        foreach (PartialStageHunkItem hunk in Hunks)
        {
            hunk.IsSelected = true;
        }
    }

    /// <summary>Clears the current hunk selection.</summary>
    public void ClearSelection()
    {
        foreach (PartialStageHunkItem hunk in Hunks)
        {
            hunk.IsSelected = false;
        }
    }

    /// <summary>Applies the selected hunks to the index.</summary>
    public async Task ApplyAsync()
    {
        if (!CanApply || _patch is null)
        {
            return;
        }

        string selectedPatch = _patch.FileHeader + string.Concat(
            Hunks.Where(hunk => hunk.IsSelected).Select(hunk => hunk.Hunk.PatchText));
        ErrorMessage = string.Empty;
        IsBusy = true;
        StatusMessage = _localizer.GetString("PartialStage_Applying");
        try
        {
            await _gitService.StagePatchAsync(_repositoryPath, selectedPatch);
            WasApplied = true;
            StatusMessage = _localizer.GetString("PartialStage_Applied");
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = _localizer.GetString("Status_OperationFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void NotifyHunkState()
    {
        OnPropertyChanged(nameof(HasHunks));
        OnPropertyChanged(nameof(HasNoHunks));
        NotifySelectionChanged();
    }
}
