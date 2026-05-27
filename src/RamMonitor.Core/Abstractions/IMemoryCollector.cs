using RamMonitor.Core.Models;

namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Source des photographies de la mémoire globale du système.
/// Implémentation Windows : <c>Win32MemoryCollector</c>.
/// </summary>
public interface IMemoryCollector
{
    /// <summary>
    /// Capture l'état courant de la mémoire globale.
    /// L'opération est synchrone côté OS mais exposée en <see cref="ValueTask{TResult}"/>
    /// pour permettre des implémentations alternatives (WMI async, ETW, etc.).
    /// </summary>
    ValueTask<MemorySnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
