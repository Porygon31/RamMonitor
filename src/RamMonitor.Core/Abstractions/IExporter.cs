using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Stratégie d'export d'un <see cref="SystemSnapshot"/> vers un fichier.
/// Le projet fournit 4 implémentations : CSV, JSON, PNG, HTML.
/// </summary>
public interface IExporter
{
    /// <summary>Identifiant court ("csv", "json", "png", "html") — utilisé en command parameter.</summary>
    string FormatId { get; }

    /// <summary>Libellé affiché dans l'UI.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Extension de fichier produite (ex. ".json"). Vide si l'export crée un dossier
    /// plutôt qu'un fichier unique (ex. CSV multi-fichiers).
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Écrit l'export dans <paramref name="targetFolder"/>. Retourne le chemin
    /// produit (fichier ou dossier selon l'implémentation).
    /// </summary>
    ValueTask<string> ExportAsync(SystemSnapshot snapshot, string targetFolder,
        CancellationToken cancellationToken = default);
}
