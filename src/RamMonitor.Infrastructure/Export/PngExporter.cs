using System.Globalization;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Infrastructure.Export;

/// <summary>
/// Délégué fournissant les octets PNG du graphe courant. La couche UI WPF
/// l'injecte au démarrage (LiveCharts2 expose un <c>chart.GetImage().Encoded</c>
/// utilisable côté Views). Si non fourni, l'export est désactivé.
/// </summary>
public delegate ValueTask<byte[]?> ChartImageProvider(CancellationToken cancellationToken);

/// <summary>
/// Exporteur PNG. Délègue la capture du graphe à la couche UI via <see cref="ChartImageProvider"/>.
/// Reste dans Infrastructure pour que l'orchestration export soit cohérente.
/// </summary>
public sealed class PngExporter : IExporter
{
    private readonly ChartImageProvider? _imageProvider;

    public PngExporter(ChartImageProvider? imageProvider = null)
    {
        _imageProvider = imageProvider;
    }

    public string FormatId => "png";
    public string DisplayName => "Image PNG du graphe";
    public string FileExtension => ".png";

    public async ValueTask<string> ExportAsync(SystemSnapshot snapshot, string targetFolder,
        CancellationToken cancellationToken = default)
    {
        if (_imageProvider is null)
        {
            throw new InvalidOperationException(
                "ChartImageProvider non fourni : le PngExporter doit être enregistré depuis la couche UI.");
        }

        var bytes = await _imageProvider(cancellationToken).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            throw new InvalidOperationException("Le graphe n'a pas pu être capturé (UI non initialisée ?).");
        }

        Directory.CreateDirectory(targetFolder);
        var stamp = snapshot.Timestamp.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(targetFolder, $"rammonitor_chart_{stamp}.png");

        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return path;
    }
}
