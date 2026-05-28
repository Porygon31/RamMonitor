using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using RamMonitor.App.Composition;
using RamMonitor.Core.Services;
using RamMonitor.Core.Utils;
using SkiaSharp;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Vue principale : graphe temps réel d'utilisation RAM (en %) + cartes synthèse.
/// S'abonne aux événements FastTick / SlowTick du moteur et met à jour ses
/// collections observables sur le thread UI.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly MonitoringEngine _engine;
    private readonly AppOptions _options;
    private readonly ObservableCollection<DateTimePoint> _ramSeriesValues = new();
    private readonly ObservableCollection<DateTimePoint> _commitSeriesValues = new();

    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    /// <summary>Couleur du texte de la légende du chart.</summary>
    public SolidColorPaint LegendTextPaint { get; private set; } = null!;

    /// <summary>Fond de la légende (transparent).</summary>
    public SolidColorPaint LegendBackgroundPaint { get; private set; } = null!;

    [ObservableProperty] private string _ramUsedText = "—";
    [ObservableProperty] private string _ramAvailableText = "—";
    [ObservableProperty] private string _ramTotalText = "—";
    [ObservableProperty] private double _ramUsedPercent;
    [ObservableProperty] private string _commitText = "—";
    [ObservableProperty] private double _commitPercent;
    [ObservableProperty] private string _pagefileText = "—";
    [ObservableProperty] private double _pagefilePercent;
    [ObservableProperty] private string _standbyCacheText = "—";
    [ObservableProperty] private string _modifiedListText = "—";
    [ObservableProperty] private string _kernelPagedText = "—";
    [ObservableProperty] private string _kernelNonPagedText = "—";
    [ObservableProperty] private string _hardwareReservedText = "—";

    public DashboardViewModel(MonitoringEngine engine, AppOptions options)
    {
        _engine = engine;
        _options = options;

        Series = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Name = "RAM %",
                Values = _ramSeriesValues,
                Fill = new SolidColorPaint(new SKColor(126, 197, 255, 60)),
                Stroke = new SolidColorPaint(new SKColor(126, 197, 255)) { StrokeThickness = 2 },
                GeometrySize = 0,
                LineSmoothness = 0.4
            },
            new LineSeries<DateTimePoint>
            {
                Name = "Commit %",
                Values = _commitSeriesValues,
                Fill = null,
                Stroke = new SolidColorPaint(new SKColor(158, 228, 147)) { StrokeThickness = 1.5f },
                GeometrySize = 0,
                LineSmoothness = 0.4
            }
        };

        // Couleurs lisibles sur fond sombre — appliquées aux labels et séparateurs.
        var axisLabelPaint = new SolidColorPaint(new SKColor(230, 230, 230)) { SKTypeface = SKTypeface.Default };
        var axisSeparatorPaint = new SolidColorPaint(new SKColor(58, 61, 66, 120));

        XAxes = new[]
        {
            new Axis
            {
                Labeler = ticks => new DateTime((long)ticks).ToString("HH:mm:ss"),
                UnitWidth = TimeSpan.FromSeconds(1).Ticks,
                LabelsRotation = 0,
                MinStep = TimeSpan.FromSeconds(15).Ticks,
                LabelsPaint = axisLabelPaint,
                SeparatorsPaint = axisSeparatorPaint
            }
        };

        YAxes = new[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                Labeler = v => $"{v:F0}%",
                LabelsPaint = axisLabelPaint,
                SeparatorsPaint = axisSeparatorPaint
            }
        };

        // Texte de la légende (RAM %, Commit %) en couleur claire.
        LegendTextPaint = new SolidColorPaint(new SKColor(230, 230, 230));
        LegendBackgroundPaint = new SolidColorPaint(SKColors.Transparent);

        _engine.FastTick += OnFastTick;
        _engine.SlowTick += OnSlowTick;

        // Enregistrement du provider d'image pour le PngExporter.
        ChartImageProviderRegistry.Provider = ProvideChartImageAsync;
    }

    /// <summary>
    /// Provider d'image utilisé par le PngExporter. La View remplace cette delegate
    /// par une vraie capture dès qu'elle a une référence au chart.
    /// </summary>
    public Func<ValueTask<byte[]?>>? ChartImageCapture { get; set; }

    private ValueTask<byte[]?> ProvideChartImageAsync(CancellationToken ct)
        => ChartImageCapture is null ? ValueTask.FromResult<byte[]?>(null) : ChartImageCapture();

    private void OnFastTick(FastTickResult result)
    {
        var maxPoints = _options.Ui.ChartHistorySeconds;
        var memory = result.Memory;
        var now = memory.Timestamp.LocalDateTime;

        RunOnUi(() =>
        {
            // Ring buffer simple : append + drop le plus ancien si dépassement.
            _ramSeriesValues.Add(new DateTimePoint(now, memory.UsedPercent));
            _commitSeriesValues.Add(new DateTimePoint(now, memory.CommitPercent));

            while (_ramSeriesValues.Count > maxPoints) _ramSeriesValues.RemoveAt(0);
            while (_commitSeriesValues.Count > maxPoints) _commitSeriesValues.RemoveAt(0);

            RamUsedText = ByteFormatter.Format(memory.UsedPhysicalBytes);
            RamAvailableText = ByteFormatter.Format(memory.AvailablePhysicalBytes);
            RamTotalText = ByteFormatter.Format(memory.TotalPhysicalBytes);
            RamUsedPercent = memory.UsedPercent;
            CommitText = $"{ByteFormatter.Format(memory.CommitTotalBytes)} / {ByteFormatter.Format(memory.CommitLimitBytes)}";
            CommitPercent = memory.CommitPercent;
            PagefilePercent = memory.PagefileUsedPercent;
            PagefileText = $"{ByteFormatter.Format(memory.PageFileTotalBytes - memory.PageFileAvailableBytes)} / {ByteFormatter.Format(memory.PageFileTotalBytes)}";
            StandbyCacheText = ByteFormatter.Format(memory.StandbyCacheBytes);
            ModifiedListText = ByteFormatter.Format(memory.ModifiedPageListBytes);
            KernelPagedText = ByteFormatter.Format(memory.KernelPagedBytes);
            KernelNonPagedText = ByteFormatter.Format(memory.KernelNonPagedBytes);
            HardwareReservedText = ByteFormatter.Format(memory.HardwareReservedBytes);
        });
    }

    private void OnSlowTick(SlowTickResult result)
    {
        // Hook libre pour widgets futurs (pagefile details, standby détaillé...).
    }

    public void Dispose()
    {
        _engine.FastTick -= OnFastTick;
        _engine.SlowTick -= OnSlowTick;
    }
}
