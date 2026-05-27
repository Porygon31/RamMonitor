namespace RamMonitor.Core.Abstractions;

/// <summary>Type de dump mémoire à produire.</summary>
public enum DumpKind
{
    /// <summary>Mini dump : ~ quelques MB, threads + handles + stacks.</summary>
    MiniDump = 0,

    /// <summary>Full dump : inclut toute la mémoire mappée (gros fichier).</summary>
    FullDump = 1
}

/// <summary>
/// Production d'un dump mémoire (.dmp) d'un process.
/// L'élévation + <c>SeDebugPrivilege</c> sont nécessaires pour les process
/// système ou ceux d'autres utilisateurs.
/// </summary>
public interface IMemoryDumpService
{
    /// <summary>
    /// Crée le dump dans <paramref name="targetFolder"/>. Retourne le chemin
    /// complet du fichier produit, ou <c>null</c> en cas d'échec.
    /// </summary>
    ValueTask<string?> CreateDumpAsync(int processId, DumpKind kind, string targetFolder,
        CancellationToken cancellationToken = default);
}
