namespace RamMonitor.Infrastructure.Interop;

/// <summary>
/// Constantes Win32 utilisées par les appels P/Invoke. Les valeurs proviennent
/// des en-têtes officiels (winnt.h, ntstatus.h, psapi.h) — ne pas les modifier.
/// </summary>
internal static class NativeConstants
{
    // ----- Process access rights (winnt.h) -----
    public const uint PROCESS_QUERY_INFORMATION       = 0x0400;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint PROCESS_VM_READ                 = 0x0010;
    public const uint PROCESS_SET_INFORMATION         = 0x0200;
    public const uint PROCESS_TERMINATE               = 0x0001;
    public const uint PROCESS_SET_QUOTA               = 0x0100;
    public const uint PROCESS_ALL_ACCESS              = 0x001F0FFF;

    // ----- Token access rights (winnt.h) -----
    public const uint TOKEN_QUERY      = 0x0008;
    public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;

    // ----- Privileges -----
    public const string SE_DEBUG_NAME              = "SeDebugPrivilege";
    public const string SE_INCREASE_QUOTA_NAME     = "SeIncreaseQuotaPrivilege";
    public const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
    public const string SE_CREATE_PAGEFILE_NAME    = "SeCreatePagefilePrivilege";

    public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    // ----- TOKEN_INFORMATION_CLASS -----
    public const int TokenElevation = 20;

    // ----- NtSetSystemInformation classes (non documenté) -----
    // Source : reverse engineering Sysinternals RAMMap. Utilisable depuis Vista+.
    public const int SystemMemoryListInformation = 80;
    public const int SystemFileCacheInformation  = 21;

    // Commandes pour SystemMemoryListInformation
    public const int MemoryEmptyWorkingSets         = 2; // Vide les working sets de tous les process
    public const int MemoryFlushModifiedList        = 3; // Flush la modified page list vers le disque
    public const int MemoryPurgeStandbyList         = 4; // Vide la standby list
    public const int MemoryPurgeLowPriorityStandbyList = 5; // Vide uniquement les pages basse priorité

    // ----- Priority classes (winbase.h) -----
    public const uint IDLE_PRIORITY_CLASS         = 0x00000040;
    public const uint BELOW_NORMAL_PRIORITY_CLASS = 0x00004000;
    public const uint NORMAL_PRIORITY_CLASS       = 0x00000020;
    public const uint ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000;
    public const uint HIGH_PRIORITY_CLASS         = 0x00000080;
    public const uint REALTIME_PRIORITY_CLASS     = 0x00000100;

    // ----- MiniDump flags (dbghelp.h) -----
    public const uint MiniDumpNormal              = 0x00000000;
    public const uint MiniDumpWithFullMemory      = 0x00000002;
    public const uint MiniDumpWithHandleData      = 0x00000004;
    public const uint MiniDumpWithThreadInfo      = 0x00001000;
    public const uint MiniDumpWithUnloadedModules = 0x00000020;

    // ----- File access -----
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint CREATE_ALWAYS = 2;

    // ----- Error codes -----
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_ACCESS_DENIED = 5;
    public const int STATUS_SUCCESS = 0;
}
