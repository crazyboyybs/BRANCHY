using Branchy.Models;

namespace Branchy.Services;

/// <summary>Persists and applies user-facing application preferences.</summary>
public interface IAppSettingsService
{
    /// <summary>Occurs after preferences have been saved.</summary>
    event EventHandler? PreferencesChanged;

    /// <summary>Gets the current preferences.</summary>
    AppPreferences Preferences { get; }

    /// <summary>Applies the saved language before resources are loaded.</summary>
    void ApplyLanguageOverride();

    /// <summary>Saves a complete preference set.</summary>
    void Save(AppPreferences preferences);
}
