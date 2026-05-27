using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Actions modificatrices exécutables sur un process (kill, priorité, affinité,
/// empty working set). Les actions sur des process d'autres utilisateurs ou
/// système exigent l'élévation et idéalement <c>SeDebugPrivilege</c>.
/// </summary>
public interface IProcessActionService
{
    /// <summary>Termine le process. Code de sortie arbitraire (1).</summary>
    ValueTask<bool> KillAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force le process à libérer sa RAM vers la standby list.
    /// Le process ré-alloue à la demande, ce qui peut causer un ralentissement temporaire.
    /// </summary>
    ValueTask<bool> EmptyWorkingSetAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>Change la priorité de scheduling du process.</summary>
    ValueTask<bool> SetPriorityAsync(int processId, ProcessPriorityLevel priority,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Définit le masque d'affinité CPU (bit i = autorisé sur le CPU i).
    /// Un masque à 0 est interdit (planterait le process) — l'implémentation le substitue
    /// par "tous les CPU".
    /// </summary>
    ValueTask<bool> SetAffinityAsync(int processId, nuint affinityMask,
        CancellationToken cancellationToken = default);
}
