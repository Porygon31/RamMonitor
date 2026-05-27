using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using System.Globalization;
using System.Text.Json;

namespace RamMonitor.Infrastructure.Export;

/// <summary>
/// Exporteur JSON. Produit un unique fichier <c>rammonitor_YYYYMMDD_HHMMSS.json</c>
/// contenant l'intégralité du SystemSnapshot. Indenté pour lisibilité humaine.
/// </summary>
public sealed class JsonExporter : IExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string FormatId => "json";
    public string DisplayName => "JSON";
    public string FileExtension => ".json";

    public async ValueTask<string> ExportAsync(SystemSnapshot snapshot, string targetFolder,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetFolder);

        var stamp = snapshot.Timestamp.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(targetFolder, $"rammonitor_{stamp}.json");

        // FileStream + sérialisation streaming : OK même pour des snapshots avec plusieurs milliers de process.
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, snapshot, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return path;
    }
}
