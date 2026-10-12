namespace Branchy.Services;

/// <summary>Provides localized application strings to non-UI layers.</summary>
public interface ILocalizationService
{
    /// <summary>Gets a localized string by resource key.</summary>
    string GetString(string key);

    /// <summary>Formats a localized string using the current UI culture.</summary>
    string Format(string key, params object[] arguments);
}
