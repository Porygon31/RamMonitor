using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using RamMonitor.App.Composition;
using RamMonitor.Core.Abstractions;
using RamMonitor.Infrastructure.Export;
using SkiaSharp;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Historique long-terme : interroge SQLite pour reconstruire la série mémoire
/// globale (et optionnellement un process spécifique) sur une fenêtre temporelle.
/// </summary>
public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly ISnapshotRepository _repository;
    private readonly ISystemSnapshotService _snapshotService;
    private readonly IEnumerable<IExporter> _exporters;
    private readonly PngExporter _pngExporter;
    private readonly AppOptions _options;
    private readonly ObservableCollection<DateTimePoint> _memorySeriesValues = new();
    private readonly ObservableCollection<DateTimePoint> _processSeriesValues = new();

    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public IReadOnlyList<TimeRangeOption> TimeRanges { get; } = new[]
    {
        new TimeRangeOption("15 minutes", TimeSpan.FromMinutes(15)),
        new TimeRangeOption("1 heure", TimeSpan.FromHours(1)),
        new TimeRangeOption("6 heures", TimeSpan.FromHours(6)),
        new TimeRangeOption("24 heures", TimeSpan.FromHours(24)),
        new TimeRangeOption("7 jours", TimeSpan.FromDays(7))
    };

    [ObservableProperty] private TimeRangeOption _selectedRange;
    [ObservableProperty] private string _processFilterPid = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isLoading;

    public HistoryViewModel(
        ISnapshotRepository repository,
        ISystemSnapshotService snapshotService,
        IEnumerable<IExporter> exporters,
        PngExporter pngExporter,
        AppOptions options)
    {
        _repository = repository;
        _snapshotService = snapshotService;
        _exporters = exporters;
        _pngExporter = pngExporter;
        _options = options;
        _selectedRange = TimeRanges[1];

        Series = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Name = "RAM %",
                Values = _memorySeriesValues,
                Stroke = new SolidColorPaint(new SKColor(126, 197, 255)) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(new SKColor(126, 197, 255, 50)),
                GeometrySize = 0
            },
            new LineSeries<DateTimePoint>
            {
                Name = "Process WS (MB)",
                Values = _processSeriesValues,
                Stroke = new SolidColorPaint(new SKColor(255, 167, 91)) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                ScalesYAt = 1
            }
        };

        XAxes = new[]
        {
            new Axis { Labeler = ticks => new DateTime((long)ticks).ToString("MM-dd HH:mm") }
        };

        YAxes = new[]
        {
            new Axis { Name = "RAM %", MinLimit = 0, MaxLimit = 100, Labeler = v => $"{v:F0}%" },
            new Axis { Name = "MB", Position = LiveChartsCore.Measure.AxisPosition.End,
                      Labeler = v => $"{v:F0} MB" }
        };
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var to = DateTimeOffset.Now;
            var from = to - SelectedRange.Duration;

            var memHistory = await _repository.GetMemoryHistoryAsync(from, to).ConfigureAwait(true);
            _memorySeriesValues.Clear();
            foreach (var snap in memHistory)
            {
                _memorySeriesValues.Add(new DateTimePoint(snap.Timestamp.LocalDateTime, snap.UsedPercent));
            }

            _processSeriesValues.Clear();
            if (int.TryParse(ProcessFilterPid, out var pid))
            {
                var procHistory = await _repository.GetProcessHistoryAsync(pid, from, to).ConfigureAwait(true);
                foreach (var p in procHistory)
                {
                    _processSeriesValues.Add(new DateTimePoint(
                        p.Timestamp.LocalDateTime,
                        p.WorkingSetBytes / (1024.0 * 1024.0)));
                }
                StatusMessage = $"{procHistory.Count} points pour PID {pid}.";
            }
            else
            {
                StatusMessage = $"{memHistory.Count} échantillons mémoire globale.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportSnapshotAsync(string formatId)
    {
        IsLoading = true;
        try
        {
            var snapshot = await _snapshotService.CaptureAsync().ConfigureAwait(true);
            var folder = Path.Combine(AppContext.BaseDirectory, _options.Export.DefaultFolder);

            IExporter exporter = formatId == "png"
                ? _pngExporter
                : _exporters.FirstOrDefault(e => e.FormatId == formatId)
                  ?? throw new InvalidOperationException($"Exporter '{formatId}' introuvable.");

            var path = await exporter.ExportAsync(snapshot, folder).ConfigureAwait(true);
            StatusMessage = $"Exporté : {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Échec export : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Option de fenêtre temporelle (label + durée).</summary>
public sealed record TimeRangeOption(string Label, TimeSpan Duration)
{
    public override string ToString() => Label;
}
