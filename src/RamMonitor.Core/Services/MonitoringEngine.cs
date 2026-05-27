using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Core.Services;

/// <summary>
/// Options de polling du <see cref="MonitoringEngine"/>.
/// Modifiables à chaud côté Settings mais ne prennent effet qu'au prochain redémarrage du moteur.
/// </summary>
public sealed class MonitoringEngineOptions
{
    /// <summary>Intervalle entre 2 ticks "rapides" (mémoire globale + process).</summary>
    public int FastIntervalMs { get; set; } = 1000;

    /// <summary>Intervalle entre 2 ticks "lents" (pagefile, leak analysis).</summary>
    public int SlowIntervalMs { get; set; } = 5000;

    /// <summary>Intervalle quand la fenêtre est minimisée et qu'on ne pause pas totalement.</summary>
    public int MinimizedIntervalMs { get; set; } = 10000;

    /// <summary>Si <c>true</c>, le polling s'arrête totalement quand la fenêtre est minimisée.</summary>
    public bool PauseWhenMinimized { get; set; } = false;
}

/// <summary>Résultat d'un tick rapide (publié via l'événement <see cref="MonitoringEngine.FastTick"/>).</summary>
public sealed record FastTickResult(MemorySnapshot Memory, IReadOnlyList<ProcessSnapshot> TopProcesses);

/// <summary>Résultat d'un tick lent.</summary>
public sealed record SlowTickResult(
    IReadOnlyList<PagefileInfo> Pagefiles,
    StandbyCacheInfo? StandbyCache,
    IReadOnlyList<LeakReport> LeakReports);

/// <summary>
/// Cœur du monitoring temps réel. Tourne 2 boucles asynchrones concurrentes :
///   - <b>Fast loop</b> (1s) : RAM globale + process + alertes + persistance,
///   - <b>Slow loop</b> (5s) : pagefile + standby + analyse de fuites.
///
/// Les ViewModels s'abonnent aux events <see cref="FastTick"/>, <see cref="SlowTick"/>
/// et <see cref="AlertRaised"/>. Le marshalling vers le thread UI est de leur responsabilité.
/// </summary>
public sealed class MonitoringEngine : IAsyncDisposable
{
    private readonly IMemoryCollector _memoryCollector;
    private readonly IProcessCollector _processCollector;
    private readonly IPagefileService _pagefileService;
    private readonly IStandbyCacheService _standbyCacheService;
    private readonly ISnapshotRepository _repository;
    private readonly IAlertEvaluator _alertEvaluator;
    private readonly IAlertNotifier _alertNotifier;
    private readonly ILeakDetector _leakDetector;
    private readonly ILogger<MonitoringEngine> _logger;

    private readonly MonitoringEngineOptions _options;

    // Séries internes pour la détection de fuites : conservées en RAM sur ~2h glissantes.
    // Stocker la totalité de l'historique en DB serait coûteux à requêter à chaque slow tick.
    private readonly Dictionary<int, List<ProcessSnapshot>> _processSeries = new();
    private readonly object _seriesLock = new();

    private CancellationTokenSource? _cts;
    private Task? _fastLoop;
    private Task? _slowLoop;
    private volatile bool _isMinimized;

    /// <summary>Émis après chaque tick rapide réussi. Le handler doit marshaller vers UI lui-même.</summary>
    public event Action<FastTickResult>? FastTick;

    /// <summary>Émis après chaque tick lent réussi.</summary>
    public event Action<SlowTickResult>? SlowTick;

    /// <summary>Émis pour chaque alerte qui passe le filtre anti-spam.</summary>
    public event Action<AlertRecord>? AlertRaised;

    public MonitoringEngine(
        IMemoryCollector memoryCollector,
        IProcessCollector processCollector,
        IPagefileService pagefileService,
        IStandbyCacheService standbyCacheService,
        ISnapshotRepository repository,
        IAlertEvaluator alertEvaluator,
        IAlertNotifier alertNotifier,
        ILeakDetector leakDetector,
        MonitoringEngineOptions options,
        ILogger<MonitoringEngine>? logger = null)
    {
        _memoryCollector = memoryCollector;
        _processCollector = processCollector;
        _pagefileService = pagefileService;
        _standbyCacheService = standbyCacheService;
        _repository = repository;
        _alertEvaluator = alertEvaluator;
        _alertNotifier = alertNotifier;
        _leakDetector = leakDetector;
        _options = options;
        _logger = logger ?? NullLogger<MonitoringEngine>.Instance;
    }

    /// <summary><c>true</c> si les loops tournent.</summary>
    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;

    /// <summary>
    /// Indique au moteur que la fenêtre est minimisée — il ralentit alors le polling
    /// selon <see cref="MonitoringEngineOptions.MinimizedIntervalMs"/> et
    /// <see cref="MonitoringEngineOptions.PauseWhenMinimized"/>.
    /// </summary>
    public void SetMinimized(bool minimized) => _isMinimized = minimized;

    /// <summary>Démarre les 2 loops. Idempotent (no-op si déjà démarré).</summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _fastLoop = Task.Run(() => FastLoopAsync(_cts.Token), _cts.Token);
        _slowLoop = Task.Run(() => SlowLoopAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("MonitoringEngine started.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Arrête les loops proprement et attend leur terminaison (timeout côté appelant si besoin).
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
            await Task.WhenAll(_fastLoop ?? Task.CompletedTask, _slowLoop ?? Task.CompletedTask)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Attendu : c'est le mécanisme normal d'arrêt.
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _fastLoop = null;
            _slowLoop = null;
            _logger.LogInformation("MonitoringEngine stopped.");
        }
    }

    /// <summary>Loop rapide : RAM globale + process + alertes + persistance.</summary>
    private async Task FastLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Pause totale si configurée et fenêtre minimisée.
                if (_isMinimized && _options.PauseWhenMinimized)
                {
                    await Task.Delay(_options.MinimizedIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Collecte + persistance.
                var memory = await _memoryCollector.CollectAsync(cancellationToken).ConfigureAwait(false);
                var processes = await _processCollector.CollectAllAsync(cancellationToken).ConfigureAwait(false);

                await _repository.SaveMemoryAsync(memory, cancellationToken).ConfigureAwait(false);
                await _repository.SaveProcessesAsync(processes, cancellationToken).ConfigureAwait(false);

                // Alimente les séries internes pour le leak detector (slow loop).
                AppendToSeries(processes);

                // Évaluation des alertes + notification + persistance.
                var alerts = _alertEvaluator.Evaluate(memory, processes, DateTimeOffset.Now);
                foreach (var alert in alerts)
                {
                    await _repository.SaveAlertAsync(alert, cancellationToken).ConfigureAwait(false);
                    await _alertNotifier.NotifyAsync(alert, cancellationToken).ConfigureAwait(false);
                    AlertRaised?.Invoke(alert);
                }

                FastTick?.Invoke(new FastTickResult(memory, processes));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Une exception isolée ne doit pas tuer la loop : on log et on continue.
                _logger.LogError(ex, "Fast loop tick failed.");
            }

            try
            {
                // Si la fenêtre est minimisée mais qu'on ne pause pas, on ralentit.
                var delay = _isMinimized && !_options.PauseWhenMinimized
                    ? _options.MinimizedIntervalMs
                    : _options.FastIntervalMs;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Loop lente : pagefile + standby + analyse de fuites.</summary>
    private async Task SlowLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var pagefilesTask = _pagefileService.GetAllAsync(cancellationToken);
                StandbyCacheInfo? standby = null;
                try
                {
                    standby = await _standbyCacheService.GetAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Le standby cache nécessite l'élévation : c'est un échec attendu en non-admin.
                    _logger.LogDebug(ex, "Standby cache details unavailable (likely requires admin).");
                }

                var pagefiles = await pagefilesTask.ConfigureAwait(false);
                var leaks = RunLeakAnalysis();

                // Émet une alerte par leak suspecté (l'anti-spam de l'évaluateur fait son travail).
                foreach (var leak in leaks)
                {
                    var leakAlerts = _alertEvaluator.EvaluateLeak(leak, DateTimeOffset.Now);
                    foreach (var alert in leakAlerts)
                    {
                        await _repository.SaveAlertAsync(alert, cancellationToken).ConfigureAwait(false);
                        await _alertNotifier.NotifyAsync(alert, cancellationToken).ConfigureAwait(false);
                        AlertRaised?.Invoke(alert);
                    }
                }

                SlowTick?.Invoke(new SlowTickResult(pagefiles, standby, leaks));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Slow loop tick failed.");
            }

            try
            {
                await Task.Delay(_options.SlowIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Met à jour les séries glissantes (2h max). Les process disparus depuis &gt; 2h
    /// sont retirés pour éviter une croissance mémoire indéfinie.
    /// </summary>
    private void AppendToSeries(IReadOnlyList<ProcessSnapshot> processes)
    {
        var threshold = DateTimeOffset.Now - TimeSpan.FromHours(2);

        lock (_seriesLock)
        {
            foreach (var p in processes)
            {
                if (!_processSeries.TryGetValue(p.ProcessId, out var list))
                {
                    list = new List<ProcessSnapshot>(128);
                    _processSeries[p.ProcessId] = list;
                }

                list.Add(p);

                // Drop des points trop anciens (purge à la première occurrence — pas à chaque tick).
                if (list.Count > 0 && list[0].Timestamp < threshold)
                {
                    list.RemoveAll(s => s.Timestamp < threshold);
                }
            }

            // PIDs dont aucun snapshot récent n'est arrivé : à oublier.
            var stale = _processSeries
                .Where(kv => kv.Value.Count == 0 || kv.Value[^1].Timestamp < threshold)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var pid in stale)
            {
                _processSeries.Remove(pid);
            }
        }
    }

    /// <summary>Lance l'analyse de fuite sur la copie des séries internes (verrou minimal).</summary>
    private IReadOnlyList<LeakReport> RunLeakAnalysis()
    {
        Dictionary<int, IReadOnlyList<ProcessSnapshot>> snapshot;
        Dictionary<int, string> names;

        // Copie sous verrou pour libérer la loop fast au plus vite.
        lock (_seriesLock)
        {
            snapshot = _processSeries.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<ProcessSnapshot>)kv.Value.ToArray());
            names = _processSeries
                .Where(kv => kv.Value.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value[^1].Name);
        }

        return _leakDetector.AnalyzeAll(snapshot, names);
    }

    /// <summary>Dispose async : assure l'arrêt propre des loops.</summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
