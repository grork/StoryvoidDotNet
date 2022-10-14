using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System.Diagnostics;

namespace Codevoid.Storyvoid.Converters;

/// <summary>
/// Converts string into a <see cref="Visibility"/> value. Non-null, non-empty
/// becomes <see cref="Visibility.Visible"/>, all others
/// <see cref="Visibility.Collapsed"/>
/// </summary>
internal class StringVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(targetType == typeof(Visibility));

        var stringValue = value as String;

        return (String.IsNullOrEmpty(stringValue) ? Visibility.Collapsed : Visibility.Visible);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
