using System.Globalization;

namespace RamMonitor.Core.Utils;

/// <summary>
/// Formatage humain des tailles en octets (base 1024 : KB, MB, GB, TB, PB).
/// Utilise systématiquement la culture invariante (point comme séparateur décimal)
/// pour garantir un format stable indépendant de la machine.
/// </summary>
public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Convertit un nombre d'octets en chaîne humaine "X.YZ UNIT".
    /// Ex: <c>1572864 → "1.5 MB"</c>.
    /// </summary>
    public static string Format(ulong bytes, int decimals = 2)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        var size = (double)bytes;
        var unitIndex = 0;
        while (size >= 1024.0 && unitIndex < Units.Length - 1)
        {
            size /= 1024.0;
            unitIndex++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(size, decimals)} {Units[unitIndex]}");
    }

    /// <summary>
    /// Version signée. Préfixe par <c>-</c> si négatif, puis applique <see cref="Format(ulong,int)"/>
    /// sur la valeur absolue.
    /// </summary>
    public static string Format(long bytes, int decimals = 2)
    {
        var sign = bytes < 0 ? "-" : string.Empty;
        var abs = bytes < 0 ? (ulong)(-bytes) : (ulong)bytes;
        return sign + Format(abs, decimals);
    }

    /// <summary>
    /// Formate un taux en octets/minute (utilisé pour la pente du LeakDetector).
    /// Ex: <c>1572864 → "1.5 MB/min"</c>.
    /// </summary>
    public static string FormatRatePerMinute(double bytesPerMinute, int decimals = 2)
    {
        var sign = bytesPerMinute < 0 ? "-" : string.Empty;
        var abs = Math.Abs(bytesPerMinute);
        var size = abs;
        var unitIndex = 0;
        while (size >= 1024.0 && unitIndex < Units.Length - 1)
        {
            size /= 1024.0;
            unitIndex++;
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"{sign}{Math.Round(size, decimals)} {Units[unitIndex]}/min");
    }
}
