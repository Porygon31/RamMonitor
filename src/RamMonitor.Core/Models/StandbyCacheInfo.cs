namespace RamMonitor.Core.Models;

/// <summary>
/// Détails sur le standby cache et les listes de pages du Memory Manager Windows.
/// Source des données : <c>NtQuerySystemInformation(SystemMemoryListInformation)</c> — non documenté.
/// </summary>
public sealed record StandbyCacheInfo
{
    /// <summary>Total du standby cache (toutes priorités confondues).</summary>
    public required ulong StandbyCacheBytes { get; init; }

    /// <summary>Standby cache "normal" (priorités 1 à 7).</summary>
    public required ulong StandbyCacheNormalPriorityBytes { get; init; }

    /// <summary>Standby cache "reserve" (priorité 0 — cache de boot, faible coût de réutilisation).</summary>
    public required ulong StandbyCacheReserveBytes { get; init; }

    /// <summary>Modified page list (pages dirty en attente d'écriture pagefile/disque).</summary>
    public required ulong ModifiedPageListBytes { get; init; }

    /// <summary>Free page list (libres, non zéroées).</summary>
    public required ulong FreePageListBytes { get; init; }

    /// <summary>Zero page list (libres et déjà zéroées, prêtes à allouer).</summary>
    public required ulong ZeroPageListBytes { get; init; }
}
