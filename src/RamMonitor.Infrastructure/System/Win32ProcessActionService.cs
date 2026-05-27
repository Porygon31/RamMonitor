using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Infrastructure.Interop;

namespace RamMonitor.Infrastructure.System;

/// <summary>
/// Actions modificatrices sur un process. Les actions ciblant un process
/// d'un autre utilisateur ou un process système exigent l'élévation et
/// idéalement SeDebugPrivilege.
/// </summary>
public sealed class Win32ProcessActionService : IProcessActionService
{
    private readonly ILogger<Win32ProcessActionService> _logger;
    private readonly PrivilegeHelper _privilegeHelper;
    private bool _debugPrivilegeAttempted;

    public Win32ProcessActionService(ILogger<Win32ProcessActionService>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32ProcessActionService>.Instance;
        _privilegeHelper = new PrivilegeHelper(_logger);
    }

    public ValueTask<bool> KillAsync(int processId, CancellationToken cancellationToken = default)
    {
        EnsureDebugPrivilegeAttempted();

        using var handle = NativeMethods.OpenProcess(NativeConstants.PROCESS_TERMINATE, false, (uint)processId);
        if (handle.IsInvalid)
        {
            _logger.LogWarning("OpenProcess(PROCESS_TERMINATE) failed for PID {Pid}.", processId);
            return ValueTask.FromResult(false);
        }

        // Code de sortie 1 = "Terminé par RamMonitor". Convention arbitraire (non zéro).
        var ok = NativeMethods.TerminateProcess(handle, 1);
        if (!ok)
        {
            _logger.LogError("TerminateProcess failed for PID {Pid}.", processId);
        }

        return ValueTask.FromResult(ok);
    }

    public ValueTask<bool> EmptyWorkingSetAsync(int processId, CancellationToken cancellationToken = default)
    {
        EnsureDebugPrivilegeAttempted();

        // QUOTA requis (l'API EmptyWorkingSet modifie les quotas du process cible).
        using var handle = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_QUERY_INFORMATION | NativeConstants.PROCESS_SET_QUOTA,
            false, (uint)processId);

        if (handle.IsInvalid)
        {
            _logger.LogWarning("OpenProcess failed for EmptyWorkingSet (PID {Pid}).", processId);
            return ValueTask.FromResult(false);
        }

        var ok = NativeMethods.EmptyWorkingSet(handle);
        return ValueTask.FromResult(ok);
    }

    public ValueTask<bool> SetPriorityAsync(int processId, ProcessPriorityLevel priority,
        CancellationToken cancellationToken = default)
    {
        EnsureDebugPrivilegeAttempted();

        using var handle = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_SET_INFORMATION, false, (uint)processId);

        if (handle.IsInvalid)
        {
            return ValueTask.FromResult(false);
        }

        var priorityClass = priority switch
        {
            ProcessPriorityLevel.Idle        => NativeConstants.IDLE_PRIORITY_CLASS,
            ProcessPriorityLevel.BelowNormal => NativeConstants.BELOW_NORMAL_PRIORITY_CLASS,
            ProcessPriorityLevel.Normal      => NativeConstants.NORMAL_PRIORITY_CLASS,
            ProcessPriorityLevel.AboveNormal => NativeConstants.ABOVE_NORMAL_PRIORITY_CLASS,
            ProcessPriorityLevel.High        => NativeConstants.HIGH_PRIORITY_CLASS,
            ProcessPriorityLevel.Realtime    => NativeConstants.REALTIME_PRIORITY_CLASS,
            _ => NativeConstants.NORMAL_PRIORITY_CLASS
        };

        // REALTIME peut figer la machine si mal utilisé : on logge un warning.
        if (priority == ProcessPriorityLevel.Realtime)
        {
            _logger.LogWarning("Setting REALTIME priority on PID {Pid} — system may become unresponsive.", processId);
        }

        return ValueTask.FromResult(NativeMethods.SetPriorityClass(handle, priorityClass));
    }

    public ValueTask<bool> SetAffinityAsync(int processId, nuint affinityMask,
        CancellationToken cancellationToken = default)
    {
        EnsureDebugPrivilegeAttempted();

        using var handle = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_SET_INFORMATION | NativeConstants.PROCESS_QUERY_INFORMATION,
            false, (uint)processId);

        if (handle.IsInvalid)
        {
            return ValueTask.FromResult(false);
        }

        // Masque 0 interdit (planterait le process). On y substitue une valeur "tous les CPU".
        if (affinityMask == 0)
        {
            affinityMask = (nuint)((1UL << Environment.ProcessorCount) - 1);
        }

        return ValueTask.FromResult(NativeMethods.SetProcessAffinityMask(handle, affinityMask));
    }

    private void EnsureDebugPrivilegeAttempted()
    {
        // SeDebug permet d'agir sur les process d'autres utilisateurs (et certains process système).
        // On tente une fois pour la durée de vie du service.
        if (_debugPrivilegeAttempted)
        {
            return;
        }

        _debugPrivilegeAttempted = true;
        _privilegeHelper.TryEnable(NativeConstants.SE_DEBUG_NAME);
    }
}
