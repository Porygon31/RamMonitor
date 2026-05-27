namespace RamMonitor.Core.Models;

/// <summary>
/// Résultat d'une analyse de fuite mémoire pour un process donné, produit
/// par <see cref="Abstractions.ILeakDetector"/> via régression linéaire.
/// </summary>
public sealed record LeakReport
{
    /// <summary>PID du process analysé.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Nom du process au moment de l'analyse.</summary>
    public required string ProcessName { get; init; }

    /// <summary>Début de la fenêtre temporelle analysée.</summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>Fin de la fenêtre (typiquement <c>DateTimeOffset.Now</c>).</summary>
    public required DateTimeOffset WindowEnd { get; init; }

    /// <summary>Nombre d'échantillons utilisés par la régression.</summary>
    public required int SampleCount { get; init; }

    /// <summary>Pente de la régression : croissance moyenne du Working Set en octets/minute.</summary>
    public required double GrowthRateBytesPerMinute { get; init; }

    /// <summary>R² de la régression : qualité de l'ajustement linéaire (0..1).</summary>
    public required double RSquared { get; init; }

    /// <summary>Working Set au début de la fenêtre.</summary>
    public required ulong StartWorkingSetBytes { get; init; }

    /// <summary>Working Set à la fin de la fenêtre.</summary>
    public required ulong EndWorkingSetBytes { get; init; }

    /// <summary>
    /// <c>true</c> si la fuite est suspectée (combinaison pente &gt; seuil ET R² &gt; seuil).
    /// </summary>
    public required bool IsSuspectedLeak { get; init; }
}
