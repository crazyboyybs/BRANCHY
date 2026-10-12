using Branchy.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Branchy.Converters;

/// <summary>Shows the visual that corresponds to a semantic diff-line kind.</summary>
public sealed class DiffLineKindToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is GitDiffLineKind kind &&
        Enum.TryParse(parameter?.ToString(), out GitDiffLineKind expected) &&
        kind == expected
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
