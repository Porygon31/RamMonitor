using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Source des photographies par process.
/// Implémentation Windows : <c>Win32ProcessCollector</c>.
/// </summary>
public interface IProcessCollector
{
    /// <summary>
    /// Énumère tous les process accessibles. Les process système qu'on ne peut pas ouvrir
    /// sont retournés avec leurs métriques de base (sans pools), pas filtrés.
    /// </summary>
    ValueTask<IReadOnlyList<ProcessSnapshot>> CollectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Récupère un process unique par PID. Retourne <c>null</c> si le PID n'existe pas
    /// ou si le process est inaccessible.
    /// </summary>
    ValueTask<ProcessSnapshot?> CollectByIdAsync(int processId, CancellationToken cancellationToken = default);
}
