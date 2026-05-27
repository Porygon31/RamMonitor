using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Détecte les fuites mémoire en analysant la série temporelle du Working Set
/// d'un process via régression linéaire (pente + R²).
/// </summary>
public interface ILeakDetector
{
    /// <summary>
    /// Analyse les échantillons d'un process unique. Retourne <c>null</c> si la série
    /// est trop courte. Sinon retourne un <see cref="LeakReport"/> dont
    /// <see cref="LeakReport.IsSuspectedLeak"/> indique si une fuite est suspectée.
    /// </summary>
    LeakReport? Analyze(int processId, string processName, IReadOnlyList<ProcessSnapshot> samples);

    /// <summary>
    /// Analyse plusieurs process en une passe. Ne retourne que les process pour
    /// lesquels la série est analysable.
    /// </summary>
    IReadOnlyList<LeakReport> AnalyzeAll(IReadOnlyDictionary<int, IReadOnlyList<ProcessSnapshot>> samplesByPid,
        IReadOnlyDictionary<int, string> processNames);
}
