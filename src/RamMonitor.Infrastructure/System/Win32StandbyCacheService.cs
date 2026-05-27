using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Infrastructure.Interop;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace RamMonitor.Infrastructure.System;

/// <summary>
/// Service de gestion du standby cache et des working sets globaux.
/// Toutes les opérations d'écriture nécessitent :
///   1. Process élevé (Administrator),
///   2. Privilège <c>SeProfileSingleProcessPrivilege</c> activé sur le token.
/// </summary>
public sealed class Win32StandbyCacheService : IStandbyCacheService
{
    private readonly ILogger<Win32StandbyCacheService> _logger;
    private readonly PrivilegeHelper _privilegeHelper;
    private readonly uint _pageSize;

    public Win32StandbyCacheService(ILogger<Win32StandbyCacheService>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32StandbyCacheService>.Instance;
        _privilegeHelper = new PrivilegeHelper(_logger);

        // PageSize fixe pour la durée du process (généralement 4 KB).
        var perf = new NativeStructs.PERFORMANCE_INFORMATION
        {
            cb = (uint)Marshal.SizeOf<NativeStructs.PERFORMANCE_INFORMATION>()
        };
        NativeMethods.GetPerformanceInfo(out perf, perf.cb);
        _pageSize = (uint)perf.PageSize;
    }

    public ValueTask<StandbyCacheInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Tenter d'activer le privilège : silencieux si refusé.
        _privilegeHelper.TryEnable(NativeConstants.SE_PROF_SINGLE_PROCESS_NAME);

        var size = (uint)Marshal.SizeOf<NativeStructs.SYSTEM_MEMORY_LIST_INFORMATION>();
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var status = NativeMethods.NtQuerySystemInformation(
                NativeConstants.SystemMemoryListInformation, buffer, size, out _);

            if (status != NativeConstants.STATUS_SUCCESS)
            {
                throw new InvalidOperationException(
                    $"NtQuerySystemInformation failed (NTSTATUS 0x{status:X8}). Élévation requise.");
            }

            var info = Marshal.PtrToStructure<NativeStructs.SYSTEM_MEMORY_LIST_INFORMATION>(buffer);
            var ps = (ulong)_pageSize;

            // Standby normal = priorités 1 à 7 (priorité 0 = "reserve", utilisée pour le cache de boot).
            ulong normalPages =
                (ulong)info.PageCountByPriority1 + (ulong)info.PageCountByPriority2 +
                (ulong)info.PageCountByPriority3 + (ulong)info.PageCountByPriority4 +
                (ulong)info.PageCountByPriority5 + (ulong)info.PageCountByPriority6 +
                (ulong)info.PageCountByPriority7;
            ulong reservePages = (ulong)info.PageCountByPriority0;
            ulong totalStandby = normalPages + reservePages;

            return ValueTask.FromResult(new StandbyCacheInfo
            {
                StandbyCacheBytes = totalStandby * ps,
                StandbyCacheNormalPriorityBytes = normalPages * ps,
                StandbyCacheReserveBytes = reservePages * ps,
                ModifiedPageListBytes = (ulong)info.ModifiedPageCount * ps,
                FreePageListBytes = (ulong)info.FreePageCount * ps,
                ZeroPageListBytes = (ulong)info.ZeroPageCount * ps
            });
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public ValueTask<bool> ClearStandbyListAsync(CancellationToken cancellationToken = default)
        => InvokeMemoryCommandAsync(NativeConstants.MemoryPurgeStandbyList);

    public ValueTask<bool> ClearModifiedPageListAsync(CancellationToken cancellationToken = default)
        => InvokeMemoryCommandAsync(NativeConstants.MemoryFlushModifiedList);

    public ValueTask<bool> ClearWorkingSetsAsync(CancellationToken cancellationToken = default)
        => InvokeMemoryCommandAsync(NativeConstants.MemoryEmptyWorkingSets);

    private ValueTask<bool> InvokeMemoryCommandAsync(int command)
    {
        // SeProfileSingleProcessPrivilege doit être activé avant tout appel.
        if (!_privilegeHelper.TryEnable(NativeConstants.SE_PROF_SINGLE_PROCESS_NAME))
        {
            _logger.LogWarning("SeProfileSingleProcessPrivilege not held: standby operations require elevation.");
            return ValueTask.FromResult(false);
        }

        var cmd = command;
        var status = NativeMethods.NtSetSystemInformation(
            NativeConstants.SystemMemoryListInformation, ref cmd, sizeof(int));

        if (status != NativeConstants.STATUS_SUCCESS)
        {
            _logger.LogError("NtSetSystemInformation failed (NTSTATUS 0x{Status:X8}) for command {Cmd}.",
                status, command);
            return ValueTask.FromResult(false);
        }

        _logger.LogInformation("Memory list command {Cmd} executed successfully.", command);
        return ValueTask.FromResult(true);
    }
}
