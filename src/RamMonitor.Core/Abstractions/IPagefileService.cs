using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Service de lecture et modification des pagefiles.
/// Les opérations d'écriture exigent l'élévation + privilège <c>SeCreatePagefile</c>.
/// </summary>
public interface IPagefileService
{
    /// <summary>Énumère tous les pagefiles configurés sur la machine.</summary>
    ValueTask<IReadOnlyList<PagefileInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifie la taille initiale et maximale d'un pagefile existant.
    /// Retourne <c>false</c> si l'opération échoue (privilèges insuffisants, chemin invalide...).
    /// </summary>
    ValueTask<bool> UpdateAsync(string path, ulong initialSizeBytes, ulong maximumSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bascule un pagefile en mode "System managed" (Windows gère automatiquement la taille).
    /// </summary>
    ValueTask<bool> SetSystemManagedAsync(string path, CancellationToken cancellationToken = default);
}
