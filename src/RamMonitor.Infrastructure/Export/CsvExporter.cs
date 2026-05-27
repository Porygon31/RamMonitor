using System.Globalization;
using System.Text;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Infrastructure.Export;

/// <summary>
/// Exporteur CSV. Produit en réalité un dossier contenant plusieurs fichiers
/// (un par type de donnée) car un seul CSV ne peut pas raisonnablement
/// représenter mémoire globale + N process + N pagefiles.
/// Le chemin retourné est celui du dossier créé.
/// </summary>
public sealed class CsvExporter : IExporter
{
    public string FormatId => "csv";
    public string DisplayName => "CSV (dossier multi-fichiers)";
    public string FileExtension => ""; // Dossier, pas fichier

    public async ValueTask<string> ExportAsync(SystemSnapshot snapshot, string targetFolder,
        CancellationToken cancellationToken = default)
    {
        var stamp = snapshot.Timestamp.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var folder = Path.Combine(targetFolder, $"rammonitor_{stamp}_csv");
        Directory.CreateDirectory(folder);

        await WriteMemoryAsync(Path.Combine(folder, "memory.csv"), snapshot.Memory, cancellationToken)
            .ConfigureAwait(false);
        await WriteProcessesAsync(Path.Combine(folder, "processes.csv"), snapshot.Processes, cancellationToken)
            .ConfigureAwait(false);
        await WritePagefilesAsync(Path.Combine(folder, "pagefiles.csv"), snapshot.Pagefiles, cancellationToken)
            .ConfigureAwait(false);

        return folder;
    }

    private static async Task WriteMemoryAsync(string path, MemorySnapshot m, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("metric;value_bytes");
        sb.AppendLine(CultureInfo.InvariantCulture, $"timestamp;{m.Timestamp:o}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"total_physical;{m.TotalPhysicalBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"available_physical;{m.AvailablePhysicalBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"used_physical;{m.UsedPhysicalBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"commit_total;{m.CommitTotalBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"commit_limit;{m.CommitLimitBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"pagefile_total;{m.PageFileTotalBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"pagefile_available;{m.PageFileAvailableBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel_paged;{m.KernelPagedBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel_nonpaged;{m.KernelNonPagedBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"system_cache;{m.SystemCacheBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"standby_cache;{m.StandbyCacheBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"modified_page_list;{m.ModifiedPageListBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"free_zero_list;{m.FreeAndZeroPageListBytes}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"hardware_reserved;{m.HardwareReservedBytes}");

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task WriteProcessesAsync(string path, IReadOnlyList<ProcessSnapshot> processes,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        // Séparateur ';' : Excel FR-friendly. Encodage UTF-8 sans BOM par défaut.
        sb.AppendLine("pid;name;working_set;private_bytes;virtual_size;paged_pool;non_paged_pool;pagefile;handles;threads;executable_path");

        foreach (var p in processes.OrderByDescending(x => x.WorkingSetBytes))
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{p.ProcessId};{EscapeCsv(p.Name)};{p.WorkingSetBytes};{p.PrivateBytes};{p.VirtualSizeBytes};" +
                $"{p.PagedPoolBytes};{p.NonPagedPoolBytes};{p.PagefileBytes};{p.HandleCount};{p.ThreadCount};" +
                $"{EscapeCsv(p.ExecutablePath ?? string.Empty)}");
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static async Task WritePagefilesAsync(string path, IReadOnlyList<PagefileInfo> pagefiles,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("path;current_size;peak_usage;initial_size;max_size;system_managed");
        foreach (var pf in pagefiles)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{EscapeCsv(pf.Path)};{pf.CurrentSizeBytes};{pf.PeakUsageBytes};" +
                $"{pf.InitialSizeBytes};{pf.MaximumSizeBytes};{pf.IsSystemManaged}");
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
    }

    private static string EscapeCsv(string s)
    {
        // Échappement RFC 4180 simplifié pour le séparateur ';'.
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
