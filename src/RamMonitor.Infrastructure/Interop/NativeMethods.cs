using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RamMonitor.Infrastructure.Interop;

/// <summary>
/// Signatures P/Invoke pour les APIs Windows nécessaires au monitoring.
/// Utilise <c>LibraryImport</c> (.NET 7+) pour des stubs zero-overhead générés
/// par source generator, sauf quand <c>DllImport</c> reste plus simple.
/// </summary>
internal static partial class NativeMethods
{
    // ===================== kernel32.dll =====================

    /// <summary>Récupère les statistiques mémoire globales. Toujours initialiser <c>dwLength</c>.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalMemoryStatusEx(ref NativeStructs.MEMORYSTATUSEX lpBuffer);

    /// <summary>Ouvre un handle vers un process par PID.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial SafeProcessHandle OpenProcess(uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateProcess(SafeProcessHandle hProcess, uint uExitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetPriorityClass(SafeProcessHandle hProcess, uint dwPriorityClass);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessAffinityMask(SafeProcessHandle hProcess, nuint dwProcessAffinityMask);

    /// <summary>Récupère le handle du process courant (pseudo-handle, pas besoin de fermer).</summary>
    [LibraryImport("kernel32.dll")]
    public static partial IntPtr GetCurrentProcess();

    /// <summary>Crée un fichier — utilisé pour le dump mémoire.</summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    // ===================== psapi.dll =====================

    /// <summary>
    /// Statistiques de performance système (mémoire + handles + threads).
    /// Les compteurs sont en pages (multiplier par <c>PageSize</c>).
    /// </summary>
    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPerformanceInfo(out NativeStructs.PERFORMANCE_INFORMATION pPerformanceInformation,
        uint cb);

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetProcessMemoryInfo(SafeProcessHandle hProcess,
        out NativeStructs.PROCESS_MEMORY_COUNTERS_EX ppsmemCounters, uint cb);

    /// <summary>
    /// Force un process à libérer son working set vers la standby list.
    /// Le process ré-allouera ses pages à la demande (peut causer un ralentissement temporaire).
    /// </summary>
    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyWorkingSet(SafeProcessHandle hProcess);

    // ===================== advapi32.dll (tokens + privilèges) =====================

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out SafeTokenHandle TokenHandle);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool LookupPrivilegeValue(string? lpSystemName, string lpName,
        out NativeStructs.LUID lpLuid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AdjustTokenPrivileges(SafeTokenHandle TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref NativeStructs.TOKEN_PRIVILEGES NewState, uint BufferLength,
        IntPtr PreviousState, IntPtr ReturnLength);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(SafeTokenHandle TokenHandle, int TokenInformationClass,
        out NativeStructs.TOKEN_ELEVATION TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    // ===================== ntdll.dll =====================

    /// <summary>
    /// API non documentée : permet d'interroger / modifier les structures internes du Memory Manager.
    /// Utilisée par Sysinternals RAMMap. Statut négatif = erreur NT (NTSTATUS).
    /// </summary>
    [DllImport("ntdll.dll", SetLastError = false)]
    public static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation,
        uint SystemInformationLength, out uint ReturnLength);

    [DllImport("ntdll.dll", SetLastError = false)]
    public static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation,
        uint SystemInformationLength);

    // ===================== dbghelp.dll =====================

    /// <summary>
    /// Génère un dump mémoire d'un process (mini ou full). Requiert SeDebugPrivilege
    /// pour cibler les process système.
    /// </summary>
    [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool MiniDumpWriteDump(SafeProcessHandle hProcess, uint ProcessId,
        SafeFileHandle hFile, uint DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam,
        IntPtr CallbackParam);
}

/// <summary>Wrapper SafeHandle pour les access tokens.</summary>
internal sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeTokenHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        // CloseHandle pour les access tokens (cf. doc OpenProcessToken).
        return Kernel32CloseHandle(handle);
    }

    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Kernel32CloseHandle(IntPtr hObject);
}
