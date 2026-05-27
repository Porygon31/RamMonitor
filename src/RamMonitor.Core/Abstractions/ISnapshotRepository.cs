using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Persistance des snapshots et alertes en séries temporelles.
/// Implémentation par défaut : <c>SqliteSnapshotRepository</c>.
/// </summary>
public interface ISnapshotRepository
{
    /// <summary>
    /// Initialise la base (création du schéma si absent). Doit être appelé
    /// une fois au démarrage avant toute autre opération.
    /// </summary>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Insère un snapshot mémoire global.</summary>
    ValueTask SaveMemoryAsync(MemorySnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Insère N snapshots de process (transaction unique côté implémentation).</summary>
    ValueTask SaveProcessesAsync(IReadOnlyList<ProcessSnapshot> snapshots,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste une alerte (utilisée pour reconstituer l'historique).</summary>
    ValueTask SaveAlertAsync(AlertRecord alert, CancellationToken cancellationToken = default);

    /// <summary>Renvoie les snapshots mémoire globaux dans une fenêtre temporelle, triés croissants.</summary>
    ValueTask<IReadOnlyList<MemorySnapshot>> GetMemoryHistoryAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renvoie les snapshots d'un process spécifique. Si le process a redémarré
    /// pendant la fenêtre, les snapshots du PID recyclé sont mélangés (à charge
    /// de l'appelant de filtrer par <see cref="ProcessSnapshot.Name"/>).
    /// </summary>
    ValueTask<IReadOnlyList<ProcessSnapshot>> GetProcessHistoryAsync(int processId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Renvoie les alertes triées du plus récent au plus ancien.</summary>
    ValueTask<IReadOnlyList<AlertRecord>> GetAlertsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime toutes les données antérieures au seuil donné (rétention).
    /// Appelé au démarrage et à la demande depuis Settings.
    /// </summary>
    ValueTask PurgeOlderThanAsync(DateTimeOffset threshold, CancellationToken cancellationToken = default);
}
