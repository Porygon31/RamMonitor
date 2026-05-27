using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Infrastructure.Interop;

namespace RamMonitor.Infrastructure.Collectors;

/// <summary>
/// Collecteur de snapshots par process. Utilise <see cref="Process.GetProcesses"/>
/// pour la liste, puis enrichit chaque entrée avec <c>GetProcessMemoryInfo</c>
/// (compteurs détaillés Working Set, Private, Pagefile, Pools).
/// Les process système inaccessibles sont retournés avec leurs métriques de base
/// (sans détails de pools) plutôt que filtrés silencieusement.
/// </summary>
public sealed class Win32ProcessCollector : IProcessCollector
{
    private readonly ILogger<Win32ProcessCollector> _logger;
    private readonly string _currentUserName;

    public Win32ProcessCollector(ILogger<Win32ProcessCollector>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32ProcessCollector>.Instance;
        _currentUserName = Environment.UserName;
    }

    public ValueTask<IReadOnlyList<ProcessSnapshot>> CollectAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.Now;
        var processes = Process.GetProcesses();
        var results = new List<ProcessSnapshot>(processes.Length);

        foreach (var proc in processes)
        {
            try
            {
                results.Add(BuildSnapshot(proc, now));
            }
            catch (Exception ex)
            {
                // Les process protégés (System, csrss, lsass...) lèvent souvent Win32Exception.
                // On loggue en Debug pour ne pas spammer.
                _logger.LogDebug(ex, "Skipped process {Pid}.", proc.Id);
            }
            finally
            {
                proc.Dispose();
            }
        }

        return ValueTask.FromResult<IReadOnlyList<ProcessSnapshot>>(results);
    }

    public ValueTask<ProcessSnapshot?> CollectByIdAsync(int processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var proc = Process.GetProcessById(processId);
            return ValueTask.FromResult<ProcessSnapshot?>(BuildSnapshot(proc, DateTimeOffset.Now));
        }
        catch (ArgumentException)
        {
            // Le PID n'existe plus.
            return ValueTask.FromResult<ProcessSnapshot?>(null);
        }
    }

    private ProcessSnapshot BuildSnapshot(Process proc, DateTimeOffset now)
    {
        // Valeurs accessibles sans handle privilégié.
        var workingSet = (ulong)Math.Max(0L, proc.WorkingSet64);
        var privateBytes = (ulong)Math.Max(0L, proc.PrivateMemorySize64);
        var virtualSize = (ulong)Math.Max(0L, proc.VirtualMemorySize64);

        ulong pagedPool = 0;
        ulong nonPagedPool = 0;
        ulong pagefile = 0;
        string? executablePath = null;
        var isSystem = false;

        // Tentative d'enrichissement avec GetProcessMemoryInfo : nécessite un handle ouvert.
        using var handle = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)proc.Id);

        if (!handle.IsInvalid)
        {
            var counters = new NativeStructs.PROCESS_MEMORY_COUNTERS_EX
            {
                cb = (uint)Marshal.SizeOf<NativeStructs.PROCESS_MEMORY_COUNTERS_EX>()
            };

            if (NativeMethods.GetProcessMemoryInfo(handle, out counters, counters.cb))
            {
                pagedPool = (ulong)counters.QuotaPagedPoolUsage;
                nonPagedPool = (ulong)counters.QuotaNonPagedPoolUsage;
                pagefile = (ulong)counters.PagefileUsage;
                // Si GetProcessMemoryInfo a réussi mais que les valeurs principales étaient 0
                // (peut arriver pour certains process), on garde celles obtenues par Process.
                if (workingSet == 0) workingSet = (ulong)counters.WorkingSetSize;
                if (privateBytes == 0) privateBytes = (ulong)counters.PrivateUsage;
            }

            // MainModule.FileName peut lever AccessDenied → try/catch ciblé.
            try { executablePath = proc.MainModule?.FileName; }
            catch { /* process protégé */ }
        }
        else
        {
            // Si on n'a pas pu ouvrir le handle, c'est presque certainement un process système.
            isSystem = true;
        }

        return new ProcessSnapshot
        {
            Timestamp = now,
            ProcessId = proc.Id,
            Name = proc.ProcessName,
            WorkingSetBytes = workingSet,
            PrivateBytes = privateBytes,
            VirtualSizeBytes = virtualSize,
            PagedPoolBytes = pagedPool,
            NonPagedPoolBytes = nonPagedPool,
            PagefileBytes = pagefile,
            HandleCount = (uint)Math.Max(0, proc.HandleCount),
            ThreadCount = (uint)proc.Threads.Count,
            ExecutablePath = executablePath,
            UserName = isSystem ? "SYSTEM" : null, // L'enrichissement complet exige WMI/OpenProcessToken — coûteux.
            IsElevated = false, // Idem : pas évalué par défaut pour rester rapide.
            IsSystem = isSystem
        };
    }
}
