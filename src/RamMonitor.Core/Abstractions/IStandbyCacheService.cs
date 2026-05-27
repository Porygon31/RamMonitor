using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Inspection et purge du standby cache et des working sets globaux.
/// Toutes les opérations d'écriture exigent l'élévation + privilège
/// <c>SeProfileSingleProcess</c>. API non documentée par Microsoft.
/// </summary>
public interface IStandbyCacheService
{
    /// <summary>Lit les détails du standby cache (priorités, modified list, etc.).</summary>
    ValueTask<StandbyCacheInfo> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Vide la standby list (libère la RAM en cache). Admin requis.</summary>
    ValueTask<bool> ClearStandbyListAsync(CancellationToken cancellationToken = default);

    /// <summary>Flush des modified pages vers le pagefile. Admin requis.</summary>
    ValueTask<bool> ClearModifiedPageListAsync(CancellationToken cancellationToken = default);

    /// <summary>Force tous les process à libérer leur Working Set. Admin requis.</summary>
    ValueTask<bool> ClearWorkingSetsAsync(CancellationToken cancellationToken = default);
}
