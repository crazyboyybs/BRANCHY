using Branchy.Models;
using Branchy.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Branchy;

/// <summary>Lets the user change application-wide preferences.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly IAppSettingsService _settings;
    private readonly AppLanguage _initialLanguage;
    private bool _isInitialized;
    private bool _isReturning;

    /// <summary>Initializes the settings surface from persisted values.</summary>
    public SettingsPage()
    {
        IAppSettingsService settings = ((App)Application.Current).Settings;
        _settings = settings;
        _initialLanguage = settings.Preferences.Language;
        InitializeComponent();

        LanguageComboBox.SelectedIndex = (int)settings.Preferences.Language;
        ThemeComboBox.SelectedIndex = (int)settings.Preferences.Theme;
        LayoutComboBox.SelectedIndex = (int)settings.Preferences.Layout;
        _isInitialized = true;
    }

    private void Preference_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        var preferences = new AppPreferences(
            (AppLanguage)Math.Max(0, LanguageComboBox.SelectedIndex),
            (AppTheme)Math.Max(0, ThemeComboBox.SelectedIndex),
            (AppLayout)Math.Max(0, LayoutComboBox.SelectedIndex));
        _settings.Save(preferences);
        RestartInfoBar.IsOpen = preferences.Language != _initialLanguage;
    }

    private void SettingsContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool compact = e.NewSize.Width < 620;
        UpdateSettingLayout(LanguageSettingGrid, LanguageComboBox, compact);
        UpdateSettingLayout(ThemeSettingGrid, ThemeComboBox, compact);
        UpdateSettingLayout(LayoutSettingGrid, LayoutComboBox, compact);
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isReturning) return;
        _isReturning = true;
        try
        {
            AnimatedIcon.SetState(BackAnimatedIcon, "Pressed");
            await Task.Delay(180);
            ((MainWindow)((App)Application.Current).MainWindow!).ReturnFromSettings();
        }
        finally
        {
            AnimatedIcon.SetState(BackAnimatedIcon, "Normal");
            _isReturning = false;
        }
    }

    private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        AnimatedIcon.SetState(BackAnimatedIcon, "PointerOver");

    private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        AnimatedIcon.SetState(BackAnimatedIcon, "Normal");

    private void BackButton_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        AnimatedIcon.SetState(BackAnimatedIcon, "Pressed");

    private void BackButton_PointerReleased(object sender, PointerRoutedEventArgs e) =>
        AnimatedIcon.SetState(BackAnimatedIcon, "PointerOver");

    private static void UpdateSettingLayout(Grid grid, ComboBox selector, bool compact)
    {
        grid.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 240);
        grid.ColumnSpacing = compact ? 0 : 20;
        Grid.SetColumnSpan((FrameworkElement)grid.Children[0], compact ? 2 : 1);
        Grid.SetRow(selector, compact ? 1 : 0);
        Grid.SetColumn(selector, compact ? 0 : 1);
        Grid.SetColumnSpan(selector, compact ? 2 : 1);
    }
}
