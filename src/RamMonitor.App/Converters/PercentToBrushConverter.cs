using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RamMonitor.App.Converters;

/// <summary>
/// Convertit un pourcentage (0..100) en brush coloré :
/// vert ≤ 70, jaune 70-90, rouge > 90.
/// Les seuils peuvent être surchargés via le paramètre "warn,crit" (ex: "75,92").
/// </summary>
public sealed class PercentToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush  = new(Color.FromRgb(158, 228, 147));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(255, 200, 87));
    private static readonly SolidColorBrush RedBrush    = new(Color.FromRgb(255, 107, 107));

    static PercentToBrushConverter()
    {
        // Brushes gelés = thread-safe et plus performants pour le rendu WPF.
        GreenBrush.Freeze();
        YellowBrush.Freeze();
        RedBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return GreenBrush;
        }

        var percent = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var (warn, crit) = ParseThresholds(parameter as string);

        if (percent >= crit) return RedBrush;
        if (percent >= warn) return YellowBrush;
        return GreenBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static (double Warn, double Crit) ParseThresholds(string? param)
    {
        if (string.IsNullOrEmpty(param))
        {
            return (70.0, 90.0);
        }

        var parts = param.Split(',', 2);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var c))
        {
            return (w, c);
        }

        return (70.0, 90.0);
    }
}
