using Branchy.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Branchy.Converters;

/// <summary>Maps planning item kinds to friendly, high-contrast accent colors.</summary>
public sealed class PlanningItemTypeToBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Color color = value switch
        {
            PlanningItemType.Bug => ColorHelper.FromArgb(255, 232, 77, 91),
            PlanningItemType.Implementation => ColorHelper.FromArgb(255, 139, 92, 246),
            PlanningItemType.Improvement => ColorHelper.FromArgb(255, 20, 184, 166),
            _ => ColorHelper.FromArgb(255, 245, 158, 11),
        };
        return new SolidColorBrush(color);
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
