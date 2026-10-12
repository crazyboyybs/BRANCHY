using Branchy.Models;
using Windows.Globalization;
using Windows.Storage;

namespace Branchy.Services;

/// <summary>Stores application preferences in the package local settings.</summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private const string LanguageKey = "Language";
    private const string ThemeKey = "Theme";
    private const string LayoutKey = "Layout";
    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    /// <inheritdoc />
    public event EventHandler? PreferencesChanged;

    /// <inheritdoc />
    public AppPreferences Preferences { get; private set; }

    /// <summary>Loads preferences saved for the current Windows user.</summary>
    public AppSettingsService()
    {
        Preferences = new AppPreferences(
            ReadEnum(LanguageKey, AppLanguage.System),
            ReadEnum(ThemeKey, AppTheme.System),
            ReadEnum(LayoutKey, AppLayout.Comfortable));
    }

    /// <inheritdoc />
    public void ApplyLanguageOverride()
    {
        ApplicationLanguages.PrimaryLanguageOverride = Preferences.Language switch
        {
            AppLanguage.PortugueseBrazil => "pt-BR",
            AppLanguage.EnglishUnitedStates => "en-US",
            _ => string.Empty,
        };
    }

    /// <inheritdoc />
    public void Save(AppPreferences preferences)
    {
        Preferences = preferences;
        _settings.Values[LanguageKey] = preferences.Language.ToString();
        _settings.Values[ThemeKey] = preferences.Theme.ToString();
        _settings.Values[LayoutKey] = preferences.Layout.ToString();
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    private T ReadEnum<T>(string key, T fallback)
        where T : struct, Enum
    {
        return _settings.Values.TryGetValue(key, out object? value) &&
               value is string text &&
               Enum.TryParse(text, ignoreCase: false, out T parsed)
            ? parsed
            : fallback;
    }
}
