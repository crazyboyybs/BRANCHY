using System.ComponentModel;
using System.Runtime.InteropServices;
using Branchy.ViewModels;
using Branchy.Services;
using Branchy.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI.Composition.SystemBackdrops;

namespace Branchy;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private const int SwRestore = 9;
    private readonly ILocalizationService _localizer = new LocalizationService();
    private bool _presenterHadBorder;
    private bool _presenterHadTitleBar;
    private int _modalDialogDepth;
    private MainViewModel? _mainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = _localizer.GetString("WindowTitle");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1280, 820));

        ApplyPreferences();

        RootFrame.Navigate(typeof(MainPage));
        AttachRepositoryTitle();
        Closed += MainWindow_Closed;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the repository name displayed under the BRANCHY title.</summary>
    public string RepositorySubtitle => string.IsNullOrWhiteSpace(_mainViewModel?.RepositoryName)
        ? _localizer.GetString("TitleBar_NoRepository")
        : _mainViewModel.RepositoryName;

    /// <summary>Applies preferences that affect the complete visual tree.</summary>
    public void ApplyPreferences()
    {
        AppPreferences preferences = ((App)Application.Current).Settings.Preferences;
        RootWindowContent.RequestedTheme = preferences.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    /// <summary>Opens standalone settings while retaining the current workspace.</summary>
    public void ShowSettingsPage()
    {
        if (RootFrame.SourcePageType == typeof(SettingsPage)) return;
        RootFrame.Navigate(typeof(SettingsPage), null,
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    /// <summary>Returns from settings to the previous workspace without reopening the repository.</summary>
    public void ReturnFromSettings()
    {
        if (RootFrame.SourcePageType == typeof(SettingsPage) && RootFrame.CanGoBack)
        {
            RootFrame.GoBack(new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromLeft,
            });
        }
    }

    /// <summary>Restores and activates the window after an external shell launch.</summary>
    public void BringToForeground()
    {
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (windowHandle == 0)
        {
            return;
        }

        if (IsIconic(windowHandle))
        {
            ShowWindow(windowHandle, SwRestore);
        }

        BringWindowToTop(windowHandle);
        SetForegroundWindow(windowHandle);
    }

    /// <summary>Shows a Desktop Acrylic dialog with native modal caption handling.</summary>
    public async Task<ContentDialogResult> ShowModalDialogAsync(ContentDialog dialog, XamlRoot xamlRoot)
    {
        dialog.XamlRoot = xamlRoot;
        object originalTemplate = dialog.ReadLocalValue(Control.TemplateProperty);
        if (DesktopAcrylicController.IsSupported())
        {
            dialog.Template = (ControlTemplate)Application.Current.Resources["DesktopAcrylicDialogTemplate"];
        }
        EnterModalState();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            ExitModalState();
            if (originalTemplate == DependencyProperty.UnsetValue)
            {
                dialog.ClearValue(Control.TemplateProperty);
            }
            else
            {
                dialog.SetValue(Control.TemplateProperty, originalTemplate);
            }
        }
    }

    private void EnterModalState()
    {
        _modalDialogDepth++;
        if (_modalDialogDepth != 1 || AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        _presenterHadBorder = presenter.HasBorder;
        _presenterHadTitleBar = presenter.HasTitleBar;
        presenter.SetBorderAndTitleBar(_presenterHadBorder, hasTitleBar: false);
    }

    private void ExitModalState()
    {
        if (_modalDialogDepth == 0 || --_modalDialogDepth != 0 ||
            AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        presenter.SetBorderAndTitleBar(_presenterHadBorder, _presenterHadTitleBar);
    }

    private void AttachRepositoryTitle()
    {
        if (RootFrame.Content is not MainPage page)
        {
            return;
        }

        _mainViewModel = page.ViewModel;
        _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RepositorySubtitle)));
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RepositoryName))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RepositorySubtitle)));
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Closed -= MainWindow_Closed;
        if (_mainViewModel is not null)
        {
            _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
            // The cached workspace survives settings navigation; dispose only when the window closes.
            _mainViewModel.Dispose();
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
