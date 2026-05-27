using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Core.Services;

/// <summary>
/// Compose un <see cref="SystemSnapshot"/> complet en orchestrant tous les
/// collecteurs en parallèle quand c'est sans danger. Le standby cache peut
/// échouer si l'app n'est pas élevée : on swallow et on continue.
/// </summary>
public sealed class SystemSnapshotService : ISystemSnapshotService
{
    private readonly IMemoryCollector _memoryCollector;
    private readonly IProcessCollector _processCollector;
    private readonly IPagefileService _pagefileService;
    private readonly IStandbyCacheService _standbyCacheService;
    private readonly IElevationService _elevationService;

    public SystemSnapshotService(
        IMemoryCollector memoryCollector,
        IProcessCollector processCollector,
        IPagefileService pagefileService,
        IStandbyCacheService standbyCacheService,
        IElevationService elevationService)
    {
        _memoryCollector = memoryCollector;
        _processCollector = processCollector;
        _pagefileService = pagefileService;
        _standbyCacheService = standbyCacheService;
        _elevationService = elevationService;
    }

    /// <inheritdoc />
    public async ValueTask<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        // Lancement en parallèle des 3 collecteurs principaux.
        var memoryTask = _memoryCollector.CollectAsync(cancellationToken);
        var processesTask = _processCollector.CollectAllAsync(cancellationToken);
        var pagefilesTask = _pagefileService.GetAllAsync(cancellationToken);

        var memory = await memoryTask.ConfigureAwait(false);
        var processes = await processesTask.ConfigureAwait(false);
        var pagefiles = await pagefilesTask.ConfigureAwait(false);

        // Le standby cache exige une élévation. Si on n'est pas admin, l'API throw —
        // on absorbe l'erreur et on retourne null pour ce champ optionnel.
        StandbyCacheInfo? standby = null;
        try
        {
            standby = await _standbyCacheService.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Volontairement silencieux : c'est un champ best-effort.
        }

        return new SystemSnapshot
        {
            Timestamp = DateTimeOffset.Now,
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.VersionString,
            UserName = Environment.UserName,
            IsElevated = _elevationService.IsCurrentProcessElevated,
            Memory = memory,
            Processes = processes,
            Pagefiles = pagefiles,
            StandbyCache = standby
        };
    }
}
