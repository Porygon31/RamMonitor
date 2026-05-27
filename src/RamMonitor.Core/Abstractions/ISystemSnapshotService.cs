using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Assemble un <see cref="SystemSnapshot"/> complet en orchestrant tous les collecteurs
/// (mémoire, process, pagefiles, standby cache). Utilisé pour l'export.
/// </summary>
public interface ISystemSnapshotService
{
    /// <summary>
    /// Capture l'état complet du système. Les sous-collecteurs sont appelés
    /// en parallèle quand c'est sans danger.
    /// </summary>
    ValueTask<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
