namespace RamMonitor.Core.Models;

/// <summary>
/// Photographie de l'état mémoire d'un process à un instant donné.
/// </summary>
public sealed record ProcessSnapshot
{
    /// <summary>Date et heure de capture.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Identifiant Windows du process (PID).</summary>
    public required int ProcessId { get; init; }

    /// <summary>Nom court du process (sans extension), tel que renvoyé par <c>Process.ProcessName</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Working Set — RAM physique réellement allouée au process.</summary>
    public required ulong WorkingSetBytes { get; init; }

    /// <summary>Private Bytes — mémoire engagée non partagée (équiv. "Commit Size" du Task Manager).</summary>
    public required ulong PrivateBytes { get; init; }

    /// <summary>Taille de l'espace d'adressage virtuel réservé par le process.</summary>
    public required ulong VirtualSizeBytes { get; init; }

    /// <summary>Quota paged pool consommé par le process.</summary>
    public required ulong PagedPoolBytes { get; init; }

    /// <summary>Quota non-paged pool consommé.</summary>
    public required ulong NonPagedPoolBytes { get; init; }

    /// <summary>Espace pagefile actuellement utilisé par le process.</summary>
    public required ulong PagefileBytes { get; init; }

    /// <summary>Nombre de handles ouverts.</summary>
    public required uint HandleCount { get; init; }

    /// <summary>Nombre de threads vivants.</summary>
    public required uint ThreadCount { get; init; }

    /// <summary>Chemin complet de l'exécutable. <c>null</c> si inaccessible (process protégé).</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>Nom du user propriétaire (rarement renseigné — coûteux à résoudre).</summary>
    public string? UserName { get; init; }

    /// <summary><c>true</c> si le process tourne en mode élevé (admin).</summary>
    public bool IsElevated { get; init; }

    /// <summary><c>true</c> si c'est un process système non accessible en lecture/écriture.</summary>
    public bool IsSystem { get; init; }
}
