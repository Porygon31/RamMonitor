namespace RamMonitor.Core.Models;

/// <summary>
/// Photographie instantanée de la mémoire globale du système.
/// Toutes les valeurs sont exprimées en octets (sauf les propriétés <c>*Percent</c>).
/// Immuable (record) : peut être partagé entre threads sans synchronisation.
/// </summary>
public sealed record MemorySnapshot
{
    /// <summary>Date et heure de la capture (timezone locale conservée).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>RAM physique totale visible par l'OS (hors "hardware reserved").</summary>
    public required ulong TotalPhysicalBytes { get; init; }

    /// <summary>RAM immédiatement disponible (zero list + free list + une partie du standby).</summary>
    public required ulong AvailablePhysicalBytes { get; init; }

    /// <summary>RAM utilisée = Total - Available.</summary>
    public required ulong UsedPhysicalBytes { get; init; }

    /// <summary>Commit charge utilisée (RAM + pagefile demandés par les process).</summary>
    public required ulong CommitTotalBytes { get; init; }

    /// <summary>Commit limit = RAM + tailles cumulées de tous les pagefiles.</summary>
    public required ulong CommitLimitBytes { get; init; }

    /// <summary>Taille totale du / des pagefile(s).</summary>
    public required ulong PageFileTotalBytes { get; init; }

    /// <summary>Espace pagefile encore disponible.</summary>
    public required ulong PageFileAvailableBytes { get; init; }

    /// <summary>Pool kernel pageable.</summary>
    public required ulong KernelPagedBytes { get; init; }

    /// <summary>Pool kernel non-pageable (toujours en RAM physique).</summary>
    public required ulong KernelNonPagedBytes { get; init; }

    /// <summary>System cache (cache de fichiers du gestionnaire de mémoire).</summary>
    public required ulong SystemCacheBytes { get; init; }

    /// <summary>Standby cache (pages réutilisables mais encore valides).</summary>
    public required ulong StandbyCacheBytes { get; init; }

    /// <summary>Modified page list (pages modifiées en attente d'écriture sur disque).</summary>
    public required ulong ModifiedPageListBytes { get; init; }

    /// <summary>Free + zero page list (pages immédiatement allouables).</summary>
    public required ulong FreeAndZeroPageListBytes { get; init; }

    /// <summary>RAM réservée matériellement (BIOS), invisible par l'OS.</summary>
    public required ulong HardwareReservedBytes { get; init; }

    /// <summary>Pourcentage RAM utilisée (0..100). Calculé à la volée.</summary>
    public double UsedPercent =>
        TotalPhysicalBytes == 0 ? 0.0 : UsedPhysicalBytes * 100.0 / TotalPhysicalBytes;

    /// <summary>Pourcentage du commit utilisé par rapport à la limite.</summary>
    public double CommitPercent =>
        CommitLimitBytes == 0 ? 0.0 : CommitTotalBytes * 100.0 / CommitLimitBytes;

    /// <summary>Pourcentage d'utilisation du pagefile.</summary>
    public double PagefileUsedPercent
    {
        get
        {
            if (PageFileTotalBytes == 0)
            {
                return 0.0;
            }

            var used = PageFileTotalBytes - PageFileAvailableBytes;
            return used * 100.0 / PageFileTotalBytes;
        }
    }
}
