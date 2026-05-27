using System.Globalization;
using System.Windows.Data;
using RamMonitor.Core.Utils;

namespace RamMonitor.App.Converters;

/// <summary>
/// Bind des octets bruts vers une représentation humaine.
/// </summary>
public sealed class BytesToHumanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ulong u => ByteFormatter.Format(u),
            long l  => ByteFormatter.Format(l),
            int i   => ByteFormatter.Format((long)i),
            _       => string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
