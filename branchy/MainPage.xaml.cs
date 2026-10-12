using Branchy.Models;
using Branchy.Services;
using Branchy.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.ComponentModel;
using System.Windows.Input;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Branchy;

/// <summary>Displays the task-oriented repository workspace.</summary>
public sealed partial class MainPage : Page
{
    private const double RepositoryHeaderSpacing = 12;
    private const double HeaderCommandGap = 12;
    private const double HeaderCommandBarMinimumWidth = 48;
    private const double RepositoryInfoMaximumWidth = 260;
    private const double RepositoryTitleDefaultFontSize = 16;
    private const double RepositoryTitleMinimumFontSize = 13;
    private static readonly TimeSpan SettingsClickAnimationDuration = TimeSpan.FromMilliseconds(180);
    private readonly ILocalizationService _localizer;
    private readonly IGitService _gitService;
    private readonly IRepositoryPickerService _repositoryPicker;
    private readonly IAppSettingsService _settings;
    private bool _isSettingsDialogOpen;
    private bool _isSynchronizingChangesSelector;
    private bool _isOverflowStyleUpdateQueued;
    private bool _isPlanningVisible;
    private bool _hasInitialized;
    private bool _isDiffExpanded;
    private Storyboard? _diffLayoutStoryboard;
    private EventHandler<object>? _diffLayoutCompleted;
    private readonly UISettings _uiSettings = new();
    private string _planningRepositoryPath = string.Empty;
    private Button? _styledOverflowButton;

    /// <summary>Initializes the page and its platform-independent ViewModel.</summary>
    public MainPage()
    {
        _localizer = new LocalizationService();
        _gitService = new GitService(_localizer);
        _repositoryPicker = new RepositoryPickerService(
            () => ((App)Application.Current).MainWindow);
        _settings = ((App)Application.Current).Settings;
        ViewModel = new MainViewModel(
            _gitService,
            _repositoryPicker,
            _localizer);

        InitializeComponent();
        // Move the existing namescope into a cached page without duplicating bindings or ViewModels.
        RootLayout.Children.Remove(HomeSurface);
        WorkspaceFrame.Navigate(typeof(HomePage), HomeSurface, new SuppressNavigationTransitionInfo());
        ApplyPreferences();
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    /// <summary>Gets the ViewModel used by compiled XAML bindings.</summary>
    public MainViewModel ViewModel { get; }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _settings.PreferencesChanged += Settings_PreferencesChanged;
        ApplyPreferences();
        if (!_hasInitialized)
        {
            _hasInitialized = true;
            var app = (App)Application.Current;
            await ViewModel.InitializeAsync(
                app.InitialRepositoryPath,
                app.HasInitialRepositoryRequest);
        }
        UpdateCommitButtonLayout();
        SynchronizeChangesSelector(animate: false);
        QueueRepositoryHeaderLayoutUpdate();
        QueueNavigationOverflowStyleUpdate();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _settings.PreferencesChanged -= Settings_PreferencesChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.RepositoryName) or
            nameof(MainViewModel.RepositoryPath) or
            nameof(MainViewModel.BranchName))
        {
            QueueRepositoryHeaderLayoutUpdate();
        }

        if (e.PropertyName == nameof(MainViewModel.RepositoryPath) && _isPlanningVisible &&
            WorkspaceFrame.SourcePageType == typeof(PlanningPage))
        {
            if (ViewModel.HasRepository)
            {
                NavigatePlanningPage();
            }
            else
            {
                HidePlanningPage();
            }
        }

        if (e.PropertyName == nameof(MainViewModel.CanShowAmend))
        {
            UpdateCommitButtonLayout();
        }

        if (e.PropertyName == nameof(MainViewModel.ChangesTabIndex))
        {
            SynchronizeChangesSelector(animate: true);
        }
    }

    private void UpdateCommitButtonLayout()
    {
        bool showAmend = ViewModel.CanShowAmend;
        CommitAmendColumn.Width = showAmend
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        Grid.SetColumnSpan(CommitPrimaryButton, showAmend ? 1 : 2);
    }

    private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueRepositoryHeaderLayoutUpdate();

    private void BranchManagerButton_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueRepositoryHeaderLayoutUpdate();

    private void HeaderCommandBar_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueRepositoryHeaderLayoutUpdate();

    private void AppNavigationView_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueNavigationOverflowStyleUpdate();

    private void QueueNavigationOverflowStyleUpdate()
    {
        if (_isOverflowStyleUpdateQueued)
        {
            return;
        }

        _isOverflowStyleUpdateQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _isOverflowStyleUpdateQueued = false;
            Button? overflowButton = FindVisualChild<Button>(
                AppNavigationView,
                "TopNavOverflowButton");
            if (overflowButton is not null &&
                !ReferenceEquals(_styledOverflowButton, overflowButton))
            {
                string? accessibleName = AutomationProperties.GetName(overflowButton);
                if (string.IsNullOrWhiteSpace(accessibleName) &&
                    overflowButton.Content is string contentName)
                {
                    accessibleName = contentName;
                }

                overflowButton.Style = (Style)Resources["RoundedTopNavigationOverflowButtonStyle"];
                overflowButton.Content = new FontIcon
                {
                    FontSize = 18,
                    Glyph = "\uE712",
                };
                if (!string.IsNullOrWhiteSpace(accessibleName))
                {
                    AutomationProperties.SetName(overflowButton, accessibleName);
                }

                _styledOverflowButton = overflowButton;
            }
        });
    }

    private void AppNavigationView_ItemInvoked(
        NavigationView sender,
        NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string action)
        {
            return;
        }

        if (sender.PaneDisplayMode != NavigationViewPaneDisplayMode.Top)
        {
            sender.IsPaneOpen = false;
        }

        var overflowButton = FindVisualChild<Button>(sender, "TopNavOverflowButton");
        var overflowFlyout = overflowButton?.Flyout;
        if (overflowFlyout?.IsOpen == true)
        {
            void OnOverflowClosed(object? closedSender, object closedArgs)
            {
                overflowFlyout.Closed -= OnOverflowClosed;
                DispatcherQueue.TryEnqueue(() => ExecuteNavigationAction(action, item));
            }

            overflowFlyout.Closed += OnOverflowClosed;
            overflowFlyout.Hide();
            return;
        }

        DispatcherQueue.TryEnqueue(() => ExecuteNavigationAction(action, item));
    }

    private static T? FindVisualChild<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            T? descendant = FindVisualChild<T>(child, name);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void ExecuteNavigationAction(string action, NavigationViewItem item)
    {
        if (action is not "Planning" and not "Settings")
        {
            HidePlanningPage(animate: action == "Home");
        }

        switch (action)
        {
            case "Home":
                break;
            case "Open":
                ExecuteNavigationCommand(ViewModel.OpenRepositoryCommand);
                break;
            case "Clone":
                CloneRepositoryButton_Click(item, new RoutedEventArgs());
                break;
            case "Refresh":
                ExecuteNavigationCommand(ViewModel.RefreshCommand);
                break;
            case "Remotes":
                RemoteManagerButton_Click(item, new RoutedEventArgs());
                break;
            case "Fetch":
                ExecuteNavigationCommand(ViewModel.FetchCommand);
                break;
            case "Pull":
                ExecuteNavigationCommand(ViewModel.PullCommand);
                break;
            case "Push":
                ExecuteNavigationCommand(ViewModel.PushCommand);
                break;
            case "History":
                HistoryButton_Click(item, new RoutedEventArgs());
                break;
            case "Planning":
                ShowPlanningPage();
                break;
            case "Tags":
                TagManagerButton_Click(item, new RoutedEventArgs());
                break;
            case "Repository":
                RepositoryOptionsButton_Click(item, new RoutedEventArgs());
                break;
            case "Settings":
                SettingsButton_Click(item, new RoutedEventArgs());
                break;
        }
    }

    private void ShowPlanningPage()
    {
        if (!ViewModel.HasRepository || (_isPlanningVisible && WorkspaceFrame.SourcePageType == typeof(PlanningPage)))
        {
            return;
        }

        _isPlanningVisible = true;
        AppNavigationView.SelectedItem = PlanningNavigationItem;
        NavigatePlanningPage();
    }

    private void NavigatePlanningPage()
    {
        if (!ViewModel.HasRepository ||
            (_planningRepositoryPath == ViewModel.RepositoryPath &&
             WorkspaceFrame.SourcePageType == typeof(PlanningPage)))
        {
            return;
        }

        _planningRepositoryPath = ViewModel.RepositoryPath;
        WorkspaceFrame.Navigate(
            typeof(PlanningPage),
            new PlanningPageContext(ViewModel.RepositoryPath, ViewModel.RepositoryName),
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void HidePlanningPage(bool animate = false)
    {
        if (!_isPlanningVisible)
        {
            return;
        }

        _isPlanningVisible = false;
        if (ReferenceEquals(AppNavigationView.SelectedItem, PlanningNavigationItem) ||
            ReferenceEquals(AppNavigationView.SelectedItem, SettingsNavigationItem))
        {
            AppNavigationView.SelectedItem = HomeNavigationItem;
        }

        WorkspaceFrame.Navigate(typeof(HomePage), HomeSurface,
            animate
                ? new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft }
                : new SuppressNavigationTransitionInfo());
    }

    private static void ExecuteNavigationCommand(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void QueueRepositoryHeaderLayoutUpdate() =>
        DispatcherQueue.TryEnqueue(UpdateRepositoryHeaderLayout);

    private void UpdateRepositoryHeaderLayout()
    {
        if (!IsLoaded || RepositoryHeaderColumn.ActualWidth <= 0)
        {
            return;
        }

        double titleNaturalWidth = MeasureTextWidth(
            RepositoryNameTextBlock,
            RepositoryTitleDefaultFontSize);
        double pathNaturalWidth = MeasureTextWidth(
            RepositoryPathTextBlock,
            RepositoryPathTextBlock.FontSize);
        double preferredInfoWidth = Math.Min(
            RepositoryInfoMaximumWidth,
            Math.Max(titleNaturalWidth, pathNaturalWidth));
        double branchWidth = Math.Max(
            BranchManagerButton.MinWidth,
            BranchManagerButton.ActualWidth);
        double reservedHeaderWidth = ViewModel.HasRepository
            ? preferredInfoWidth + RepositoryHeaderSpacing + branchWidth + HeaderCommandGap
            : 0;
        double commandBarMaximumWidth = Math.Max(
            HeaderCommandBarMinimumWidth,
            HeaderGrid.ActualWidth - reservedHeaderWidth);
        if (double.IsInfinity(HeaderCommandBar.MaxWidth) ||
            Math.Abs(HeaderCommandBar.MaxWidth - commandBarMaximumWidth) > 0.01)
        {
            HeaderCommandBar.MaxWidth = commandBarMaximumWidth;
        }

        double availableInfoWidth = Math.Max(
            0,
            RepositoryHeaderColumn.ActualWidth - RepositoryHeaderSpacing - branchWidth);
        double targetInfoWidth = Math.Min(preferredInfoWidth, availableInfoWidth);

        if (double.IsNaN(RepositoryInfoPanel.Width) ||
            Math.Abs(RepositoryInfoPanel.Width - targetInfoWidth) > 0.01)
        {
            RepositoryInfoPanel.Width = targetInfoWidth;
        }

        if (targetInfoWidth <= 0 || string.IsNullOrEmpty(RepositoryNameTextBlock.Text))
        {
            RepositoryNameTextBlock.FontSize = RepositoryTitleDefaultFontSize;
            return;
        }

        double targetFontSize = titleNaturalWidth <= targetInfoWidth
            ? RepositoryTitleDefaultFontSize
            : Math.Max(
                RepositoryTitleMinimumFontSize,
                RepositoryTitleDefaultFontSize * targetInfoWidth / titleNaturalWidth);

        if (Math.Abs(RepositoryNameTextBlock.FontSize - targetFontSize) > 0.01)
        {
            RepositoryNameTextBlock.FontSize = targetFontSize;
        }
    }

    private static double MeasureTextWidth(TextBlock source, double fontSize)
    {
        if (string.IsNullOrEmpty(source.Text))
        {
            return 0;
        }

        var measuringTextBlock = new TextBlock
        {
            Text = source.Text,
            CharacterSpacing = source.CharacterSpacing,
            FontFamily = source.FontFamily,
            FontSize = fontSize,
            FontStretch = source.FontStretch,
            FontStyle = source.FontStyle,
            FontWeight = source.FontWeight,
        };
        measuringTextBlock.Measure(
            new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return measuringTextBlock.DesiredSize.Width;
    }

    private void ChangesSelector_SelectionChanged(
        SelectorBar sender,
        SelectorBarSelectionChangedEventArgs args)
    {
        if (_isSynchronizingChangesSelector || sender.SelectedItem is null)
        {
            return;
        }

        int selectedIndex = ReferenceEquals(sender.SelectedItem, StagedSelectorItem) ? 1 : 0;
        if (ViewModel.ChangesTabIndex != selectedIndex)
        {
            ViewModel.ChangesTabIndex = selectedIndex;
            return;
        }

        NavigateChangesPage(selectedIndex, animate: true);
    }

    private void SynchronizeChangesSelector(bool animate)
    {
        int selectedIndex = ViewModel.ChangesTabIndex == 1 ? 1 : 0;
        SelectorBarItem selectedItem = selectedIndex == 1
            ? StagedSelectorItem
            : UnstagedSelectorItem;

        if (!ReferenceEquals(ChangesSelector.SelectedItem, selectedItem))
        {
            _isSynchronizingChangesSelector = true;
            ChangesSelector.SelectedItem = selectedItem;
            _isSynchronizingChangesSelector = false;
        }

        NavigateChangesPage(selectedIndex, animate);
    }

    private void NavigateChangesPage(int selectedIndex, bool animate)
    {
        Type pageType = selectedIndex == 1
            ? typeof(StagedChangesPage)
            : typeof(UnstagedChangesPage);
        if (ChangesFrame.SourcePageType == pageType)
        {
            return;
        }

        NavigationTransitionInfo transition = animate && ChangesFrame.SourcePageType is not null
            ? new SlideNavigationTransitionInfo
            {
                Effect = selectedIndex == 1
                    ? SlideNavigationTransitionEffect.FromRight
                    : SlideNavigationTransitionEffect.FromLeft,
            }
            : new SuppressNavigationTransitionInfo();
        ChangesFrame.Navigate(pageType, this, transition);
    }

    internal async Task ShowPartialStageDialogAsync(GitFileChange change)
    {
        if (!change.CanPartiallyStage)
        {
            return;
        }

        var dialog = new PartialStageDialog(
            ViewModel.RepositoryPath,
            change.Path,
            _gitService,
            _localizer);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasApplied)
        {
            await ViewModel.RefreshAsyncFromDialog();
        }
    }

    internal async Task ShowDiscardDialogAsync(GitFileChange change)
    {
        if (!change.CanDiscardWorkingTreeChange)
        {
            return;
        }

        var dialog = new DiscardChangesDialog(change, _localizer);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasConfirmed)
        {
            await ViewModel.DiscardWorkingTreeChangeAsync(change);
        }
    }

    private async void BranchManagerButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BranchManagerDialog(ViewModel);
        await ShowModalDialogAsync(dialog);
        if (dialog.RequestedMergeBranch is not GitBranch branch)
        {
            return;
        }

        var confirmation = new OperationConfirmationDialog(
            _localizer.GetString("MergeConfirmation_Title"),
            _localizer.Format("MergeConfirmation_Heading", branch.Name),
            _localizer.Format("MergeConfirmation_Message", branch.Name, ViewModel.BranchName),
            branch.Name,
            _localizer.GetString("MergeConfirmation_Action"));
        await ShowModalDialogAsync(confirmation);
        if (confirmation.WasConfirmed)
        {
            await ViewModel.MergeBranchAsync(branch);
        }
    }

    private async void RemoteManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasRepository)
        {
            return;
        }

        var dialog = new RemoteManagerDialog(
            ViewModel.RepositoryPath,
            _gitService,
            _localizer);
        await ShowModalDialogAsync(dialog);
        await ViewModel.RefreshAsyncFromDialog();
    }

    private async void CloneRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        string initialDestination = GetCloneDestinationParent();
        var dialog = new CloneRepositoryDialog(
            _gitService,
            _repositoryPicker,
            _localizer,
            initialDestination);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasCloned && !string.IsNullOrWhiteSpace(dialog.ClonedRepositoryPath))
        {
            await ViewModel.OpenRepositoryPathAsync(dialog.ClonedRepositoryPath);
        }
    }

    private async void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasRepository)
        {
            return;
        }

        var archivePicker = new ArchivePickerService(
            () => ((App)Application.Current).MainWindow,
            _localizer);
        var dialog = new HistoryDialog(
            ViewModel.RepositoryPath,
            _gitService,
            archivePicker,
            _localizer,
            allowRevert: !ViewModel.HasConflicts);
        await ShowModalDialogAsync(dialog);
        if (dialog.RequestedRevertCommit is not GitHistoryEntry commit)
        {
            return;
        }

        var confirmation = new OperationConfirmationDialog(
            _localizer.GetString("RevertConfirmation_Title"),
            _localizer.Format("RevertConfirmation_Heading", commit.ShortHash),
            _localizer.GetString("RevertConfirmation_Message"),
            _localizer.Format("RevertConfirmation_Detail", commit.ShortHash, commit.Subject),
            _localizer.GetString("RevertConfirmation_Action"));
        await ShowModalDialogAsync(confirmation);
        if (confirmation.WasConfirmed)
        {
            await ViewModel.RevertCommitAsync(commit);
        }
    }

    private async void TagManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasRepository)
        {
            return;
        }

        var dialog = new TagManagerDialog(
            ViewModel.RepositoryPath,
            _gitService,
            _localizer);
        await ShowModalDialogAsync(dialog);
        await ViewModel.RefreshAsyncFromDialog();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSettingsDialogOpen)
        {
            return;
        }

        _isSettingsDialogOpen = true;
        try
        {
            AnimatedIcon.SetState(NavigationSettingsAnimatedIcon, "Pressed");
            ((MainWindow)((App)Application.Current).MainWindow!).ShowSettingsPage();
            await Task.Delay(SettingsClickAnimationDuration);
        }
        finally
        {
            AnimatedIcon.SetState(NavigationSettingsAnimatedIcon, "Normal");
            _isSettingsDialogOpen = false;
        }
    }

    private async void RepositoryOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RepositoryOptionsDialog(
            ViewModel.RepositoryPath,
            _gitService,
            _localizer);
        await ShowModalDialogAsync(dialog);
        await ViewModel.RefreshAsyncFromDialog();
    }

    private void Diff_ExpansionChanged(object? sender, bool expanded)
    {
        if (_isDiffExpanded == expanded) return;
        double fromWidth = DiffPanel.ActualWidth;
        double fromX = DiffPanel.TransformToVisual(WorkspaceGrid).TransformPoint(new Point()).X;
        double fromOpacity = ChangesPanel.Visibility == Visibility.Visible ? ChangesPanel.Opacity : 0;
        StopDiffLayoutAnimation();
        _isDiffExpanded = expanded;
        double targetX = expanded ? 0 : ChangesColumn.ActualWidth + FirstGutterColumn.ActualWidth;
        double targetWidth = expanded ? WorkspaceGrid.ActualWidth :
            WorkspaceGrid.ActualWidth - targetX - SecondGutterColumn.ActualWidth - CommitColumn.ActualWidth;
        if (!DiffPanel.IsLoaded || !_uiSettings.AnimationsEnabled || fromWidth <= 0 || targetWidth <= 0)
        {
            CompleteDiffLayout();
            return;
        }

        // Animate real layout width rather than scaling text or capturing a stale bitmap.
        Grid.SetColumn(DiffPanel, 0);
        Grid.SetColumnSpan(DiffPanel, 5);
        Canvas.SetZIndex(DiffPanel, 1);
        DiffPanel.HorizontalAlignment = HorizontalAlignment.Left;
        DiffPanel.Width = fromWidth;
        DiffLayoutTranslation.X = fromX;
        ChangesPanel.Visibility = CommitPanel.Visibility = Visibility.Visible;
        ChangesPanel.IsHitTestVisible = CommitPanel.IsHitTestVisible = false;
        ChangesPanel.Opacity = CommitPanel.Opacity = fromOpacity;
        var duration = new Duration(TimeSpan.FromMilliseconds(240));
        var storyboard = new Storyboard();
        AddDiffLayoutAnimation(storyboard, DiffPanel, "Width", fromWidth, targetWidth, duration, dependent: true);
        AddDiffLayoutAnimation(storyboard, DiffLayoutTranslation, "X", fromX, targetX, duration);
        AddDiffLayoutAnimation(storyboard, ChangesPanel, "Opacity", fromOpacity, expanded ? 0 : 1, duration);
        AddDiffLayoutAnimation(storyboard, CommitPanel, "Opacity", fromOpacity, expanded ? 0 : 1, duration);
        _diffLayoutStoryboard = storyboard;
        _diffLayoutCompleted = (_, _) =>
        {
            if (ReferenceEquals(storyboard, _diffLayoutStoryboard)) CompleteDiffLayout();
        };
        storyboard.Completed += _diffLayoutCompleted;
        storyboard.Begin();
    }

    private static void AddDiffLayoutAnimation(Storyboard storyboard, DependencyObject target,
        string property, double from, double to, Duration duration, bool dependent = false)
    {
        var animation = new DoubleAnimation
        {
            From = from, To = to, Duration = duration, EnableDependentAnimation = dependent,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private void StopDiffLayoutAnimation()
    {
        if (_diffLayoutStoryboard is null) return;
        if (_diffLayoutCompleted is not null) _diffLayoutStoryboard.Completed -= _diffLayoutCompleted;
        _diffLayoutStoryboard.Stop();
        _diffLayoutStoryboard = null;
        _diffLayoutCompleted = null;
    }

    private void CompleteDiffLayout()
    {
        StopDiffLayoutAnimation();
        DiffPanel.Width = double.NaN;
        DiffPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        DiffLayoutTranslation.X = 0;
        Canvas.SetZIndex(DiffPanel, 0);
        ChangesPanel.Opacity = CommitPanel.Opacity = 1;
        ChangesPanel.IsHitTestVisible = CommitPanel.IsHitTestVisible = true;
        ApplyDiffLayout();
    }

    private void WorkspaceGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_diffLayoutStoryboard is not null) CompleteDiffLayout();
    }

    private void DiffPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_diffLayoutStoryboard is not null) CompleteDiffLayout();
    }

    private void ApplyDiffLayout()
    {
        ChangesPanel.Visibility = _isDiffExpanded ? Visibility.Collapsed : Visibility.Visible;
        CommitPanel.Visibility = _isDiffExpanded ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(DiffPanel, _isDiffExpanded ? 0 : 2);
        Grid.SetColumnSpan(DiffPanel, _isDiffExpanded ? 5 : 1);
    }

    private async void Diff_EditRequested(object? sender, EventArgs e)
    {
        if (!ViewModel.CanQuickEdit) return;
        string repositoryPath = ViewModel.RepositoryPath;
        string path = ViewModel.SelectedFilePath;
        try
        {
            var files = new WorkingTreeFileService(_localizer);
            WorkingTreeDocument document = await files.ReadAsync(repositoryPath, path);
            var dialog = new QuickEditDialog(repositoryPath, path, document, _localizer, XamlRoot);
            await ShowModalDialogAsync(dialog);
            if (!dialog.WasSaved || ViewModel.RepositoryPath != repositoryPath) return;
            await ViewModel.RefreshAsyncFromDialog();
            ViewModel.ChangesTabIndex = 0;
            GitFileChange? change = ViewModel.UnstagedFiles.FirstOrDefault(file => file.Path == path);
            if (change is not null) await ViewModel.SelectFileAsync(change, staged: false);
        }
        catch (Exception exception)
        {
            var error = new ContentDialog
            {
                Title = _localizer.GetString("QuickEditDialog.Title"),
                Content = exception.Message,
                CloseButtonText = _localizer.GetString("CloseRepositoryOptionsButton.Content"),
                CornerRadius = new CornerRadius(8),
            };
            await ShowModalDialogAsync(error);
        }
    }

    private async void AmendButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAmend)
        {
            return;
        }

        var dialog = new AmendCommitDialog(
            ViewModel.RepositoryPath,
            _gitService,
            _localizer);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasConfirmed && dialog.SelectedCommit is GitCommit commit)
        {
            await ViewModel.AmendAsync(commit, dialog.CommitMessage);
        }
    }

    private async void AmendMessageButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAmendMessage)
        {
            return;
        }

        var dialog = new AmendCommitDialog(
            ViewModel.RepositoryPath,
            _gitService,
            _localizer,
            messageOnly: true);
        await ShowModalDialogAsync(dialog);
        if (dialog.WasConfirmed && dialog.SelectedCommit is GitCommit commit)
        {
            await ViewModel.AmendMessageAsync(commit, dialog.CommitMessage);
        }
    }

    private Task<ContentDialogResult> ShowModalDialogAsync(ContentDialog dialog)
    {
        if (((App)Application.Current).MainWindow is not MainWindow window)
        {
            throw new InvalidOperationException(_localizer.GetString("Error_MainWindowUnavailable"));
        }

        return window.ShowModalDialogAsync(dialog, XamlRoot);
    }

    private string GetCloneDestinationParent()
    {
        if (ViewModel.HasRepository)
        {
            string? parent = Directory.GetParent(ViewModel.RepositoryPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents)
            ? documents
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private void Settings_PreferencesChanged(object? sender, EventArgs e) => ApplyPreferences();

    private void ApplyPreferences()
    {
        if (_diffLayoutStoryboard is not null) CompleteDiffLayout();
        if (((App)Application.Current).MainWindow is MainWindow window)
        {
            window.ApplyPreferences();
        }

        bool compact = _settings.Preferences.Layout == AppLayout.Compact;
        WorkspaceGrid.Margin = compact ? new Thickness(8, 4, 8, 8) : new Thickness(16, 8, 16, 12);
        HeaderGrid.Margin = compact ? new Thickness(8, 0, 8, 0) : new Thickness(16, 0, 16, 0);
        ChangesColumn.Width = new GridLength(compact ? 300 : 340);
        CommitColumn.Width = new GridLength(compact ? 280 : 320);
        FirstGutterColumn.Width = new GridLength(compact ? 8 : 12);
        SecondGutterColumn.Width = new GridLength(compact ? 8 : 12);
    }

    private void SettingsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSettingsDialogOpen)
        {
            AnimatedIcon.SetState(NavigationSettingsAnimatedIcon, "PointerOver");
        }
    }

    private void SettingsButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSettingsDialogOpen)
        {
            AnimatedIcon.SetState(NavigationSettingsAnimatedIcon, "Normal");
        }
    }

    private void SettingsButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSettingsDialogOpen)
        {
            AnimatedIcon.SetState(NavigationSettingsAnimatedIcon, "Pressed");
        }
    }

    private void SettingsButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSettingsDialogOpen)
        {
            AnimatedIcon.SetState(NavigationSettingsAnimatedIcon, "PointerOver");
        }
    }
}
