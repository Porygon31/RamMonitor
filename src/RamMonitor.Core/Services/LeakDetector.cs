using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Core.Utils;

namespace RamMonitor.Core.Services;

/// <summary>
/// Configuration de <see cref="LeakDetector"/>. Bindée depuis appsettings.json.
/// </summary>
public sealed class LeakDetectorOptions
{
    /// <summary>Fenêtre glissante d'analyse. Plus large = plus stable mais plus de latence à détecter.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Nombre minimum d'échantillons requis pour analyser une série.</summary>
    public int MinSamples { get; set; } = 20;

    /// <summary>Seuil de pente — au-dessus, le process est candidat à une fuite.</summary>
    public double GrowthRateBytesPerMinuteThreshold { get; set; } = 1024 * 1024;

    /// <summary>
    /// R² minimum requis. Une régression avec R² &lt; ce seuil est considérée trop
    /// bruitée pour être une fuite (probablement juste de la variation normale).
    /// </summary>
    public double MinR2 { get; set; } = 0.85;
}

/// <summary>
/// Détecteur de fuites mémoire.
/// Algorithme : régression linéaire (moindres carrés) sur le Working Set en
/// fonction du temps, sur une fenêtre glissante. Une fuite est suspectée si
/// la pente dépasse un seuil ET que R² est suffisamment élevé (ajustement bon).
/// </summary>
public sealed class LeakDetector : ILeakDetector
{
    private readonly LeakDetectorOptions _options;
    private readonly ILogger<LeakDetector> _logger;

    public LeakDetector(LeakDetectorOptions options, ILogger<LeakDetector>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<LeakDetector>.Instance;
    }

    /// <inheritdoc />
    public LeakReport? Analyze(int processId, string processName, IReadOnlyList<ProcessSnapshot> samples)
    {
        if (samples is null || samples.Count < _options.MinSamples)
        {
            return null;
        }

        // Tri par timestamp puis filtrage sur la fenêtre glissante.
        var ordered = samples.OrderBy(s => s.Timestamp).ToArray();
        var windowStart = ordered[^1].Timestamp - _options.Window;
        var windowed = ordered.Where(s => s.Timestamp >= windowStart).ToArray();

        if (windowed.Length < _options.MinSamples)
        {
            return null;
        }

        var first = windowed[0];
        var last = windowed[^1];
        var totalMinutes = (last.Timestamp - first.Timestamp).TotalMinutes;
        if (totalMinutes <= 0.0)
        {
            // Tous les échantillons au même instant : impossible de fitter.
            return null;
        }

        // Préparation des vecteurs xs (minutes depuis baseTime) et ys (working set en octets).
        var xs = new double[windowed.Length];
        var ys = new double[windowed.Length];
        var baseTime = first.Timestamp;
        for (var i = 0; i < windowed.Length; i++)
        {
            xs[i] = (windowed[i].Timestamp - baseTime).TotalMinutes;
            ys[i] = windowed[i].WorkingSetBytes;
        }

        var regression = LinearRegression.Fit(xs, ys);

        // Critères combinés : pente > seuil ET R² > seuil ET croissance nette.
        // La 3e condition rejette les cas où la régression "fitterait" une décroissance
        // suivie d'un rebond — on veut une vraie tendance haussière.
        var isLeak = regression.Slope > _options.GrowthRateBytesPerMinuteThreshold
                     && regression.RSquared >= _options.MinR2
                     && last.WorkingSetBytes > first.WorkingSetBytes;

        var report = new LeakReport
        {
            ProcessId = processId,
            ProcessName = processName,
            WindowStart = first.Timestamp,
            WindowEnd = last.Timestamp,
            SampleCount = windowed.Length,
            GrowthRateBytesPerMinute = regression.Slope,
            RSquared = regression.RSquared,
            StartWorkingSetBytes = first.WorkingSetBytes,
            EndWorkingSetBytes = last.WorkingSetBytes,
            IsSuspectedLeak = isLeak
        };

        if (isLeak)
        {
            _logger.LogWarning(
                "Leak suspected on process {Pid} '{Name}': +{Rate}/min (R²={R2:F2})",
                processId, processName, ByteFormatter.FormatRatePerMinute(regression.Slope),
                regression.RSquared);
        }

        return report;
    }

    /// <inheritdoc />
    public IReadOnlyList<LeakReport> AnalyzeAll(
        IReadOnlyDictionary<int, IReadOnlyList<ProcessSnapshot>> samplesByPid,
        IReadOnlyDictionary<int, string> processNames)
    {
        var results = new List<LeakReport>();
        foreach (var (pid, series) in samplesByPid)
        {
            var name = processNames.TryGetValue(pid, out var n) ? n : $"pid-{pid}";
            var report = Analyze(pid, name, series);
            if (report is not null)
            {
                results.Add(report);
            }
        }

        return results;
    }
}
