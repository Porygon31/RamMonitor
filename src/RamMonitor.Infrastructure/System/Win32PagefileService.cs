using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Infrastructure.System;

/// <summary>
/// Service de gestion des pagefiles. Lecture via WMI (Win32_PageFileUsage),
/// modification via Win32_PageFileSetting. Toute écriture exige l'élévation
/// + le privilège SeCreatePagefile (porté par le groupe Administrators).
/// </summary>
public sealed class Win32PagefileService : IPagefileService
{
    private readonly ILogger<Win32PagefileService> _logger;

    public Win32PagefileService(ILogger<Win32PagefileService>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32PagefileService>.Instance;
    }

    public ValueTask<IReadOnlyList<PagefileInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<PagefileInfo>();
        try
        {
            // Win32_PageFileUsage : usage en MB courant + peak.
            using var usageSearcher = new ManagementObjectSearcher(
                "SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage");

            // Win32_PageFileSetting : config (InitialSize, MaximumSize en MB).
            // Indexée par Name pour faire la jointure manuellement.
            var settings = new Dictionary<string, (uint Initial, uint Max)>(StringComparer.OrdinalIgnoreCase);
            using (var settingSearcher = new ManagementObjectSearcher(
                "SELECT Name, InitialSize, MaximumSize FROM Win32_PageFileSetting"))
            {
                foreach (var setting in settingSearcher.Get())
                {
                    var name = (string)setting["Name"];
                    var init = Convert.ToUInt32(setting["InitialSize"]);
                    var max = Convert.ToUInt32(setting["MaximumSize"]);
                    settings[name] = (init, max);
                }
            }

            foreach (var usage in usageSearcher.Get())
            {
                var name = (string)usage["Name"];
                var allocMb = Convert.ToUInt32(usage["AllocatedBaseSize"]);
                var peakMb = Convert.ToUInt32(usage["PeakUsage"]);

                settings.TryGetValue(name, out var setting);
                // Si InitialSize == 0 && MaximumSize == 0 → "System managed".
                var isSystemManaged = setting.Initial == 0 && setting.Max == 0;

                results.Add(new PagefileInfo
                {
                    Path = name,
                    CurrentSizeBytes = allocMb * 1024UL * 1024UL,
                    PeakUsageBytes = peakMb * 1024UL * 1024UL,
                    InitialSizeBytes = setting.Initial * 1024UL * 1024UL,
                    MaximumSizeBytes = setting.Max * 1024UL * 1024UL,
                    IsSystemManaged = isSystemManaged
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to enumerate pagefiles via WMI.");
        }

        return ValueTask.FromResult<IReadOnlyList<PagefileInfo>>(results);
    }

    public ValueTask<bool> UpdateAsync(string path, ulong initialSizeBytes, ulong maximumSizeBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // On manipule Win32_PageFileSetting (Initial/MaximumSize en MB).
            var query = $"SELECT * FROM Win32_PageFileSetting WHERE Name = '{path.Replace("'", "''").Replace("\\", "\\\\")}'";
            using var searcher = new ManagementObjectSearcher(query);
            using var collection = searcher.Get();

            ManagementObject? setting = null;
            foreach (var obj in collection)
            {
                setting = (ManagementObject)obj;
                break;
            }

            // Si aucune entrée n'existe (cas "System managed"), il faut en créer une.
            if (setting is null)
            {
                using var settingClass = new ManagementClass("Win32_PageFileSetting");
                setting = settingClass.CreateInstance()
                    ?? throw new InvalidOperationException("CreateInstance returned null.");
                setting["Name"] = path;
            }

            setting["InitialSize"] = (uint)(initialSizeBytes / (1024UL * 1024UL));
            setting["MaximumSize"] = (uint)(maximumSizeBytes / (1024UL * 1024UL));
            setting.Put();
            return ValueTask.FromResult(true);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            _logger.LogWarning("Access denied: pagefile update requires elevation.");
            return ValueTask.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pagefile {Path}.", path);
            return ValueTask.FromResult(false);
        }
    }

    public ValueTask<bool> SetSystemManagedAsync(string path, CancellationToken cancellationToken = default)
        // "System managed" = InitialSize = MaximumSize = 0.
        => UpdateAsync(path, 0, 0, cancellationToken);
}
