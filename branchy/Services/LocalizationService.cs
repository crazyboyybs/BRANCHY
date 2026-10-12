using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Branchy.Services;

/// <summary>Reads strings from the package resource index.</summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceLoader _resourceLoader = new();

    /// <inheritdoc />
    public string GetString(string key) => _resourceLoader.GetString(key);

    /// <inheritdoc />
    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentUICulture, GetString(key), arguments);
}
