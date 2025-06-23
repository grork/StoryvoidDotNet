using Microsoft.UI.Xaml.Data;
using System.Diagnostics;

namespace Codevoid.Storyvoid.Converters;

/// <summary>
/// Inverts a supplied boolean value e.g., false becomes true, true becomes
/// false. Intended to help with binding cases in XAML where you need to disable
/// something when a value is true.
/// </summary>
internal partial class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(targetType == typeof(bool));

        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
