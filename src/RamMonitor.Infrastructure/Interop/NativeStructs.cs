using System.Runtime.InteropServices;

namespace RamMonitor.Infrastructure.Interop;

/// <summary>
/// Structures Win32 utilisées par les P/Invoke. Le layout doit correspondre
/// exactement à celui des headers C, sinon corruption mémoire garantie.
/// </summary>
internal static class NativeStructs
{
    /// <summary>
    /// Mémoire système globale (GlobalMemoryStatusEx). Toutes les valeurs sont en octets,
    /// sauf <c>dwMemoryLoad</c> qui est un pourcentage (0-100).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    /// <summary>
    /// Statistiques de performance système (GetPerformanceInfo, psapi.h).
    /// Valeurs en PAGES (à multiplier par PageSize pour obtenir des octets).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PERFORMANCE_INFORMATION
    {
        public uint   cb;
        public nuint  CommitTotal;
        public nuint  CommitLimit;
        public nuint  CommitPeak;
        public nuint  PhysicalTotal;
        public nuint  PhysicalAvailable;
        public nuint  SystemCache;
        public nuint  KernelTotal;
        public nuint  KernelPaged;
        public nuint  KernelNonpaged;
        public nuint  PageSize;
        public uint   HandleCount;
        public uint   ProcessCount;
        public uint   ThreadCount;
    }

    /// <summary>
    /// Compteurs mémoire d'un process (GetProcessMemoryInfo).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint   cb;
        public uint   PageFaultCount;
        public nuint  PeakWorkingSetSize;
        public nuint  WorkingSetSize;
        public nuint  QuotaPeakPagedPoolUsage;
        public nuint  QuotaPagedPoolUsage;
        public nuint  QuotaPeakNonPagedPoolUsage;
        public nuint  QuotaNonPagedPoolUsage;
        public nuint  PagefileUsage;
        public nuint  PeakPagefileUsage;
        public nuint  PrivateUsage;
    }

    /// <summary>
    /// LUID — identifiant local unique pour les privilèges.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int  HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    /// <summary>
    /// TOKEN_PRIVILEGES — structure passée à AdjustTokenPrivileges.
    /// Une seule entrée pour la simplicité (suffisant pour SeDebug / SeProfileSingleProcess).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }

    /// <summary>
    /// TOKEN_ELEVATION — TokenIsElevated dans <see cref="NativeConstants.TokenElevation"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
    }

    /// <summary>
    /// Détails mémoire système avancés (NtQuerySystemInformation / SystemMemoryListInformation).
    /// Layout reverse-engineered depuis ntoskrnl. <b>Non documenté officiellement.</b>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_MEMORY_LIST_INFORMATION
    {
        public nuint ZeroPageCount;
        public nuint FreePageCount;
        public nuint ModifiedPageCount;
        public nuint ModifiedNoWritePageCount;
        public nuint BadPageCount;
        // Tableau de 8 priorités pour le standby cache (priorité 0 à 7)
        public nuint PageCountByPriority0;
        public nuint PageCountByPriority1;
        public nuint PageCountByPriority2;
        public nuint PageCountByPriority3;
        public nuint PageCountByPriority4;
        public nuint PageCountByPriority5;
        public nuint PageCountByPriority6;
        public nuint PageCountByPriority7;
        public nuint RepurposedPagesByPriority0;
        public nuint RepurposedPagesByPriority1;
        public nuint RepurposedPagesByPriority2;
        public nuint RepurposedPagesByPriority3;
        public nuint RepurposedPagesByPriority4;
        public nuint RepurposedPagesByPriority5;
        public nuint RepurposedPagesByPriority6;
        public nuint RepurposedPagesByPriority7;
        public nuint ModifiedPageCountPageFile;
    }
}
