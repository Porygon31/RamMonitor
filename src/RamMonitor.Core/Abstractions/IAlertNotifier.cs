using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Diffuse une alerte vers les canaux de notification (toast Windows, log, etc.).
/// Plusieurs implémentations peuvent coexister (composite pattern si besoin).
/// </summary>
public interface IAlertNotifier
{
    /// <summary>
    /// Envoie une notification. L'implémentation ne doit JAMAIS lever d'exception
    /// (un environnement sans toasts doit silencieusement no-op).
    /// </summary>
    ValueTask NotifyAsync(AlertRecord alert, CancellationToken cancellationToken = default);
}
