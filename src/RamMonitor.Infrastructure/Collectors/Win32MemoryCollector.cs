using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Infrastructure.Interop;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace RamMonitor.Infrastructure.Collectors;

/// <summary>
/// Collecteur de mémoire globale Windows. Combine 3 APIs pour produire un
/// <see cref="MemorySnapshot"/> complet :
///   - <c>GlobalMemoryStatusEx</c> pour la RAM physique et le pagefile,
///   - <c>GetPerformanceInfo</c> pour le commit, kernel paged/non-paged et le cache,
///   - <c>NtQuerySystemInformation</c> (non documenté) pour le standby cache détaillé.
/// </summary>
public sealed class Win32MemoryCollector : IMemoryCollector
{
    private readonly ILogger<Win32MemoryCollector> _logger;
    private readonly PrivilegeHelper _privilegeHelper;
    private readonly ulong _hardwareReservedBytes;
    private bool _profilePrivilegeAttempted;

    public Win32MemoryCollector(ILogger<Win32MemoryCollector>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32MemoryCollector>.Instance;
        _privilegeHelper = new PrivilegeHelper(_logger);

        // La RAM "Hardware Reserved" = différence entre la RAM physique installée (BIOS)
        // et celle visible par l'OS. On la calcule au démarrage car elle ne change pas.
        _hardwareReservedBytes = ComputeHardwareReserved();
    }

    public ValueTask<MemorySnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ---- 1. RAM physique + pagefile (toujours dispo, pas de privilège requis) ----
        var memStatus = new NativeStructs.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeStructs.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref memStatus))
        {
            throw new InvalidOperationException("GlobalMemoryStatusEx failed.");
        }

        // ---- 2. Commit, kernel pools, cache système (en PAGES — multiplier par PageSize) ----
        var perfInfo = new NativeStructs.PERFORMANCE_INFORMATION
        {
            cb = (uint)Marshal.SizeOf<NativeStructs.PERFORMANCE_INFORMATION>()
        };

        var perfOk = NativeMethods.GetPerformanceInfo(out perfInfo, perfInfo.cb);
        if (!perfOk)
        {
            _logger.LogWarning("GetPerformanceInfo failed; some fields will be zero.");
        }

        var pageSize = (ulong)perfInfo.PageSize;

        // ---- 3. Standby cache et listes mémoire (API non documentée) ----
        // SeProfileSingleProcessPrivilege requis : on tente une fois, en silence si refusé.
        if (!_profilePrivilegeAttempted)
        {
            _privilegeHelper.TryEnable(NativeConstants.SE_PROF_SINGLE_PROCESS_NAME);
            _profilePrivilegeAttempted = true;
        }

        ulong standby = 0, modified = 0, freeAndZero = 0;
        if (TryQueryMemoryList(out var memList))
        {
            // Le standby cache = somme des 8 priorités de pages "encore valides mais réutilisables"
            var standbyPages =
                (ulong)memList.PageCountByPriority0 + (ulong)memList.PageCountByPriority1 +
                (ulong)memList.PageCountByPriority2 + (ulong)memList.PageCountByPriority3 +
                (ulong)memList.PageCountByPriority4 + (ulong)memList.PageCountByPriority5 +
                (ulong)memList.PageCountByPriority6 + (ulong)memList.PageCountByPriority7;

            standby = standbyPages * pageSize;
            modified = (ulong)memList.ModifiedPageCount * pageSize;
            freeAndZero = ((ulong)memList.FreePageCount + (ulong)memList.ZeroPageCount) * pageSize;
        }

        var snapshot = new MemorySnapshot
        {
            Timestamp = DateTimeOffset.Now,
            TotalPhysicalBytes      = memStatus.ullTotalPhys,
            AvailablePhysicalBytes  = memStatus.ullAvailPhys,
            UsedPhysicalBytes       = memStatus.ullTotalPhys - memStatus.ullAvailPhys,
            CommitTotalBytes        = (ulong)perfInfo.CommitTotal * pageSize,
            CommitLimitBytes        = (ulong)perfInfo.CommitLimit * pageSize,
            PageFileTotalBytes      = memStatus.ullTotalPageFile,
            PageFileAvailableBytes  = memStatus.ullAvailPageFile,
            KernelPagedBytes        = (ulong)perfInfo.KernelPaged * pageSize,
            KernelNonPagedBytes     = (ulong)perfInfo.KernelNonpaged * pageSize,
            SystemCacheBytes        = (ulong)perfInfo.SystemCache * pageSize,
            StandbyCacheBytes       = standby,
            ModifiedPageListBytes   = modified,
            FreeAndZeroPageListBytes = freeAndZero,
            HardwareReservedBytes   = _hardwareReservedBytes
        };

        return ValueTask.FromResult(snapshot);
    }

    private static bool TryQueryMemoryList(out NativeStructs.SYSTEM_MEMORY_LIST_INFORMATION memList)
    {
        var size = (uint)Marshal.SizeOf<NativeStructs.SYSTEM_MEMORY_LIST_INFORMATION>();
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var status = NativeMethods.NtQuerySystemInformation(
                NativeConstants.SystemMemoryListInformation, buffer, size, out _);

            if (status != NativeConstants.STATUS_SUCCESS)
            {
                memList = default;
                return false;
            }

            memList = Marshal.PtrToStructure<NativeStructs.SYSTEM_MEMORY_LIST_INFORMATION>(buffer);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static ulong ComputeHardwareReserved()
    {
        // Hardware Reserved = TotalVisibleMemory (BIOS) - TotalPhysicalBytes (visible par l'OS).
        // On lit la taille physique installée via Win32_PhysicalMemory (WMI) si possible.
        // Si WMI échoue, on retourne 0 (l'utilisateur verra "Hardware Reserved: 0 B").
        try
        {
            using var searcher = new global::System.Management.ManagementObjectSearcher(
                "SELECT Capacity FROM Win32_PhysicalMemory");
            ulong installed = 0;
            foreach (var obj in searcher.Get())
            {
                installed += (ulong)obj["Capacity"];
            }

            var memStatus = new NativeStructs.MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<NativeStructs.MEMORYSTATUSEX>()
            };
            NativeMethods.GlobalMemoryStatusEx(ref memStatus);

            return installed > memStatus.ullTotalPhys ? installed - memStatus.ullTotalPhys : 0UL;
        }
        catch
        {
            return 0UL;
        }
    }
}
