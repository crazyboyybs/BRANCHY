using System.Collections.Specialized;
using Branchy.Models;
using Branchy.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace Branchy.Controls;

/// <summary>Provides a virtualized patch viewer with keyboard search and workspace expansion.</summary>
public sealed partial class DiffViewer : UserControl
{
    private readonly ILocalizationService _localizer = new LocalizationService();
    private IReadOnlyList<GitDiffLine> _lines = [];
    private readonly List<int> _matches = [];
    private int _matchIndex = -1;
    private INotifyCollectionChanged? _observedLines;
    private bool _updateQueued;
    private bool _isFindAnimating;
    private bool _isFindPointerOver;
    private bool _isSearchOpen;
    private Storyboard? _searchStoryboard;
    private EventHandler<object>? _searchCompleted;
    private readonly UISettings _uiSettings = new();

    /// <summary>Identifies the displayed semantic lines.</summary>
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
        nameof(Lines), typeof(IEnumerable<GitDiffLine>), typeof(DiffViewer),
        new PropertyMetadata(null, (owner, _) => ((DiffViewer)owner).ObserveLines()));

    /// <summary>Identifies whether quick editing is available.</summary>
    public static readonly DependencyProperty CanEditProperty = DependencyProperty.Register(
        nameof(CanEdit), typeof(bool), typeof(DiffViewer),
        new PropertyMetadata(false, (owner, _) => ((DiffViewer)owner).UpdateEditButton()));

    /// <summary>Identifies whether the host supports workspace expansion.</summary>
    public static readonly DependencyProperty CanExpandProperty = DependencyProperty.Register(
        nameof(CanExpand), typeof(bool), typeof(DiffViewer),
        new PropertyMetadata(true, (owner, _) => ((DiffViewer)owner).UpdateEditButton()));

    /// <summary>Gets or sets whether to offer workspace expansion.</summary>
    public bool CanExpand
    {
        get => (bool)GetValue(CanExpandProperty);
        set => SetValue(CanExpandProperty, value);
    }

    /// <summary>Gets or sets the patch lines.</summary>
    public IEnumerable<GitDiffLine>? Lines
    {
        get => (IEnumerable<GitDiffLine>?)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <summary>Gets or sets whether the working-tree file can be edited.</summary>
    public bool CanEdit
    {
        get => (bool)GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    /// <summary>Requests an expanded or normal workspace.</summary>
    public event EventHandler<bool>? ExpansionChanged;

    /// <summary>Requests the working-tree editor.</summary>
    public event EventHandler? EditRequested;

    /// <summary>Initializes viewer controls and collection lifetime tracking.</summary>
    public DiffViewer()
    {
        InitializeComponent();
        Loaded += (_, _) => ObserveLines();
        Unloaded += (_, _) =>
        {
            UnobserveLines();
            StopSearchAnimation();
            ApplySearchState();
        };
    }

    /// <summary>Returns the expansion toggle to the compact workspace state.</summary>
    public void ResetExpansion() => ExpandButton.IsChecked = false;

    private void UpdateEditButton()
    {
        if (ActionButtons is null) return;
        bool hasDiff = _lines.Count > 0;
        ActionButtons.Visibility = hasDiff ? Visibility.Visible : Visibility.Collapsed;
        if (EditButton is not null) EditButton.Visibility = CanEdit ? Visibility.Visible : Visibility.Collapsed;
        if (ExpandButton is not null) ExpandButton.Visibility = CanExpand ? Visibility.Visible : Visibility.Collapsed;
        if ((!hasDiff || !CanExpand) && ExpandButton?.IsChecked == true)
        {
            ResetExpansion();
            ExpansionChanged?.Invoke(this, false);
        }
    }

    private void ObserveLines()
    {
        UnobserveLines();
        if (IsLoaded && Lines is INotifyCollectionChanged observable)
        {
            _observedLines = observable;
            observable.CollectionChanged += Lines_CollectionChanged;
        }
        UpdateLines();
    }

    private void UnobserveLines()
    {
        if (_observedLines is not null) _observedLines.CollectionChanged -= Lines_CollectionChanged;
        _observedLines = null;
    }

    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_updateQueued) return;
        _updateQueued = DispatcherQueue.TryEnqueue(() =>
        {
            _updateQueued = false;
            if (IsLoaded) UpdateLines();
        });
    }

    private void UpdateLines()
    {
        if (LinesList is null) return;
        _lines = Lines as IReadOnlyList<GitDiffLine> ?? Lines?.ToArray() ?? [];
        LinesList.ItemsSource = _lines;
        EmptyText.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateEditButton();
        if (_lines.Count == 0)
        {
            SetSearchOpen(false, animate: false);
        }
        UpdateMatches();
    }

    private void OpenSearch()
    {
        if (_lines.Count == 0) return;
        SetSearchOpen(true);
        SearchBox.Focus(FocusState.Programmatic);
        UpdateMatches();
    }

    private void SetSearchOpen(bool open, bool animate = true)
    {
        if (_isSearchOpen == open && animate) return;
        _isSearchOpen = open;
        bool wasVisible = SearchArea.Visibility == Visibility.Visible;
        double opacity = wasVisible ? SearchArea.Opacity : 0;
        double offset = wasVisible ? SearchTranslation.Y : -8;
        // Snapshot before stopping so reversing an in-flight transition does not jump.
        StopSearchAnimation();
        SearchArea.IsHitTestVisible = open;
        if (!animate || !IsLoaded || !_uiSettings.AnimationsEnabled || !wasVisible && !open)
        {
            ApplySearchState();
            return;
        }

        SearchArea.Visibility = Visibility.Visible;
        SearchArea.Opacity = opacity;
        SearchTranslation.Y = offset;
        var duration = new Duration(TimeSpan.FromMilliseconds(open ? 220 : 150));
        var fade = new DoubleAnimation
        {
            From = opacity, To = open ? 1 : 0, Duration = duration,
            EasingFunction = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn }
        };
        var slide = new DoubleAnimation
        {
            From = offset, To = open ? 0 : -8, Duration = duration,
            EasingFunction = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fade, SearchArea);
        Storyboard.SetTargetProperty(fade, "Opacity");
        Storyboard.SetTarget(slide, SearchTranslation);
        Storyboard.SetTargetProperty(slide, "Y");
        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        _searchStoryboard = storyboard;
        // Track our animation instance, not the projected WinRT event sender.
        _searchCompleted = (_, _) =>
        {
            if (!ReferenceEquals(storyboard, _searchStoryboard)) return;
            StopSearchAnimation();
            ApplySearchState();
        };
        storyboard.Completed += _searchCompleted;
        storyboard.Begin();
    }

    private void StopSearchAnimation()
    {
        if (_searchStoryboard is null) return;
        if (_searchCompleted is not null) _searchStoryboard.Completed -= _searchCompleted;
        _searchStoryboard.Stop();
        _searchStoryboard = null;
        _searchCompleted = null;
    }

    private void ApplySearchState()
    {
        SearchArea.Visibility = _isSearchOpen ? Visibility.Visible : Visibility.Collapsed;
        SearchArea.IsHitTestVisible = _isSearchOpen;
        SearchArea.Opacity = _isSearchOpen ? 1 : 0;
        SearchTranslation.Y = 0;
        if (!_isSearchOpen) SearchBox.Text = string.Empty;
    }

    private void UpdateMatches()
    {
        _matches.Clear();
        string query = SearchBox.Text;
        if (!string.IsNullOrEmpty(query))
        {
            for (int index = 0; index < _lines.Count; index++)
                if (_lines[index].Text.Contains(query, StringComparison.OrdinalIgnoreCase)) _matches.Add(index);
        }
        _matchIndex = _matches.Count > 0 ? 0 : -1;
        RevealMatch();
    }

    private void RevealMatch()
    {
        SearchStatus.Text = _localizer.Format("Diff_SearchResults", _matchIndex + 1, _matches.Count);
        if (_matchIndex < 0) { LinesList.SelectedIndex = -1; return; }
        LinesList.SelectedIndex = _matches[_matchIndex];
        LinesList.ScrollIntoView(_lines[_matches[_matchIndex]], ScrollIntoViewAlignment.Leading);
    }

    private void MoveMatch(int direction)
    {
        if (_matches.Count == 0) return;
        _matchIndex = (_matchIndex + direction + _matches.Count) % _matches.Count;
        RevealMatch();
    }

    private async void Find_Click(object sender, RoutedEventArgs e)
    {
        OpenSearch();
        if (_isFindAnimating) return;
        _isFindAnimating = true;
        try
        {
            AnimatedIcon.SetState(FindAnimatedIcon, "Pressed");
            await Task.Delay(180);
        }
        finally
        {
            AnimatedIcon.SetState(FindAnimatedIcon, _isFindPointerOver ? "PointerOver" : "Normal");
            _isFindAnimating = false;
        }
    }

    private void Find_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isFindPointerOver = true;
        AnimatedIcon.SetState(FindAnimatedIcon, "PointerOver");
    }

    private void Find_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isFindPointerOver = false;
        AnimatedIcon.SetState(FindAnimatedIcon, "Normal");
    }

    private void Find_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        AnimatedIcon.SetState(FindAnimatedIcon, "Pressed");

    private void Find_PointerReleased(object sender, PointerRoutedEventArgs e) =>
        AnimatedIcon.SetState(FindAnimatedIcon, "PointerOver");
    private void Edit_Click(object sender, RoutedEventArgs e) => EditRequested?.Invoke(this, EventArgs.Empty);
    private void Expand_Click(object sender, RoutedEventArgs e) => ExpansionChanged?.Invoke(this, ExpandButton.IsChecked == true);
    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => UpdateMatches();
    private void Search_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => MoveMatch(1);
    private void Next_Click(object sender, RoutedEventArgs e) => MoveMatch(1);
    private void Previous_Click(object sender, RoutedEventArgs e) => MoveMatch(-1);
    private void CloseSearch_Click(object sender, RoutedEventArgs e)
    {
        SetSearchOpen(false);
        LinesList.Focus(FocusState.Programmatic);
    }
    private void Find_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        OpenSearch(); args.Handled = true;
    }
    private void FindNext_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        MoveMatch(1); args.Handled = true;
    }
    private void FindPrevious_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        MoveMatch(-1); args.Handled = true;
    }
}
