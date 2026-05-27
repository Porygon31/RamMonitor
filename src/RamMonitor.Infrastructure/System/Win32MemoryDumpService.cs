using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Infrastructure.Interop;

namespace RamMonitor.Infrastructure.System;

/// <summary>
/// Service de génération de dump mémoire (.dmp) pour un process.
/// Utilise MiniDumpWriteDump de dbghelp.dll. Pour dumper un process système ou
/// d'un autre utilisateur, le process appelant doit être élevé + SeDebugPrivilege.
/// </summary>
public sealed class Win32MemoryDumpService : IMemoryDumpService
{
    private readonly ILogger<Win32MemoryDumpService> _logger;
    private readonly PrivilegeHelper _privilegeHelper;

    public Win32MemoryDumpService(ILogger<Win32MemoryDumpService>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32MemoryDumpService>.Instance;
        _privilegeHelper = new PrivilegeHelper(_logger);
    }

    public ValueTask<string?> CreateDumpAsync(int processId, DumpKind kind, string targetFolder,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(targetFolder);

        // Activer SeDebug : permet de cibler les process système et ceux d'autres utilisateurs.
        _privilegeHelper.TryEnable(NativeConstants.SE_DEBUG_NAME);

        // PROCESS_QUERY_INFORMATION + VM_READ : minimum requis par MiniDumpWriteDump.
        using var processHandle = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_QUERY_INFORMATION | NativeConstants.PROCESS_VM_READ,
            false, (uint)processId);

        if (processHandle.IsInvalid)
        {
            _logger.LogWarning("Cannot open process {Pid} for dumping.", processId);
            return ValueTask.FromResult<string?>(null);
        }

        var fileName = $"dump_pid{processId}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.dmp";
        var fullPath = Path.Combine(targetFolder, fileName);

        // CreateFileW : GENERIC_WRITE + CREATE_ALWAYS. Pas de partage (dwShareMode = 0).
        using var fileHandle = NativeMethods.CreateFileW(
            fullPath, NativeConstants.GENERIC_WRITE, 0, IntPtr.Zero,
            NativeConstants.CREATE_ALWAYS, 0, IntPtr.Zero);

        if (fileHandle.IsInvalid)
        {
            _logger.LogError("CreateFile failed for {Path}.", fullPath);
            return ValueTask.FromResult<string?>(null);
        }

        // Mini vs Full : Full inclut TOUTE la mémoire mappée (gros fichier).
        // On enrichit toujours avec ThreadInfo + Handles + UnloadedModules.
        var flags = kind == DumpKind.FullDump
            ? NativeConstants.MiniDumpWithFullMemory
              | NativeConstants.MiniDumpWithHandleData
              | NativeConstants.MiniDumpWithThreadInfo
              | NativeConstants.MiniDumpWithUnloadedModules
            : NativeConstants.MiniDumpNormal
              | NativeConstants.MiniDumpWithHandleData
              | NativeConstants.MiniDumpWithThreadInfo;

        var ok = NativeMethods.MiniDumpWriteDump(
            processHandle, (uint)processId, fileHandle, flags,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (!ok)
        {
            _logger.LogError("MiniDumpWriteDump failed for PID {Pid}.", processId);
            try { File.Delete(fullPath); } catch { /* best-effort */ }
            return ValueTask.FromResult<string?>(null);
        }

        _logger.LogInformation("Memory dump written: {Path}", fullPath);
        return ValueTask.FromResult<string?>(fullPath);
    }
}
