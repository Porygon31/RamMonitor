using System.Globalization;
using System.Windows.Data;

namespace RamMonitor.App.Converters;

/// <summary>Inverse un bool pour binding (ex: désactiver un bouton pendant un chargement).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
