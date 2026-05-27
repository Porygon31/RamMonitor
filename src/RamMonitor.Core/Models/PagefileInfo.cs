namespace RamMonitor.Core.Models;

/// <summary>
/// Configuration d'un fichier d'échange (pagefile) sur le disque.
/// Une machine peut avoir plusieurs pagefiles (un par volume max).
/// </summary>
public sealed record PagefileInfo
{
    /// <summary>Chemin complet du fichier (ex. <c>C:\pagefile.sys</c>).</summary>
    public required string Path { get; init; }

    /// <summary>Taille actuellement allouée sur disque.</summary>
    public required ulong CurrentSizeBytes { get; init; }

    /// <summary>Pic d'utilisation depuis le boot.</summary>
    public required ulong PeakUsageBytes { get; init; }

    /// <summary>Taille initiale configurée (0 si "System managed").</summary>
    public required ulong InitialSizeBytes { get; init; }

    /// <summary>Taille maximale configurée (0 si "System managed").</summary>
    public required ulong MaximumSizeBytes { get; init; }

    /// <summary><c>true</c> si la taille est gérée automatiquement par Windows.</summary>
    public required bool IsSystemManaged { get; init; }
}
