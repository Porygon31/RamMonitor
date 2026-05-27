using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Évalue les règles d'alerte sur un snapshot et émet les alertes pertinentes.
/// L'implémentation gère l'anti-spam (intervalle minimum entre 2 alertes
/// pour une même règle / même PID).
/// </summary>
public interface IAlertEvaluator
{
    /// <summary>Règles actuellement configurées (lecture seule).</summary>
    IReadOnlyList<AlertRule> Rules { get; }

    /// <summary>Remplace l'ensemble des règles. Les compteurs anti-spam sont préservés.</summary>
    void SetRules(IEnumerable<AlertRule> rules);

    /// <summary>
    /// Évalue les règles sur un snapshot mémoire + liste de process à l'instant <paramref name="now"/>.
    /// Retourne les alertes qui doivent effectivement être émises (anti-spam appliqué).
    /// </summary>
    IReadOnlyList<AlertRecord> Evaluate(MemorySnapshot memory,
        IReadOnlyList<ProcessSnapshot> processes,
        DateTimeOffset now);

    /// <summary>Évalue spécifiquement un rapport de fuite (règle <c>LeakDetected</c>).</summary>
    IReadOnlyList<AlertRecord> EvaluateLeak(LeakReport leak, DateTimeOffset now);
}
