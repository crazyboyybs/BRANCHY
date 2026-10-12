namespace Branchy.Models;

/// <summary>Identifies the language selected for application resources.</summary>
public enum AppLanguage
{
    System,
    PortugueseBrazil,
    EnglishUnitedStates,
}

/// <summary>Identifies the color theme selected by the user.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Identifies the amount of information density in the workspace.</summary>
public enum AppLayout
{
    Comfortable,
    Compact,
}

/// <summary>Contains the persisted application preferences.</summary>
public sealed record AppPreferences(
    AppLanguage Language,
    AppTheme Theme,
    AppLayout Layout);
