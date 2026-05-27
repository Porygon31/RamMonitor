namespace RamMonitor.Core.Models;

/// <summary>
/// Photographie complète du système à un instant T, produite par
/// <see cref="Abstractions.ISystemSnapshotService"/>. Sérialisée tel quel
/// vers les formats d'export (JSON, HTML).
/// </summary>
public sealed record SystemSnapshot
{
    /// <summary>Horodatage du snapshot.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Nom de la machine.</summary>
    public required string MachineName { get; init; }

    /// <summary>Description de l'OS (sortie de <c>Environment.OSVersion</c>).</summary>
    public required string OsVersion { get; init; }

    /// <summary>Nom de l'utilisateur sous lequel tourne RamMonitor.</summary>
    public required string UserName { get; init; }

    /// <summary><c>true</c> si RamMonitor tourne en mode élevé.</summary>
    public required bool IsElevated { get; init; }

    /// <summary>État de la mémoire globale.</summary>
    public required MemorySnapshot Memory { get; init; }

    /// <summary>Tous les process (un snapshot par PID).</summary>
    public required IReadOnlyList<ProcessSnapshot> Processes { get; init; }

    /// <summary>Pagefiles configurés sur la machine.</summary>
    public required IReadOnlyList<PagefileInfo> Pagefiles { get; init; }

    /// <summary>Détails du standby cache (<c>null</c> si l'élévation n'est pas dispo).</summary>
    public StandbyCacheInfo? StandbyCache { get; init; }
}
