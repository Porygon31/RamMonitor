using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Services;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Liste des alertes (live + historique).
/// </summary>
public sealed partial class AlertsViewModel : ViewModelBase, IDisposable
{
    private readonly MonitoringEngine _engine;
    private readonly ISnapshotRepository _repository;

    public ObservableCollection<AlertRowViewModel> Alerts { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    public AlertsViewModel(MonitoringEngine engine, ISnapshotRepository repository)
    {
        _engine = engine;
        _repository = repository;
        _engine.AlertRaised += OnAlertRaised;
    }

    private void OnAlertRaised(Core.Models.AlertRecord record)
    {
        RunOnUi(() =>
        {
            Alerts.Insert(0, new AlertRowViewModel(record));

            while (Alerts.Count > 500)
            {
                Alerts.RemoveAt(Alerts.Count - 1);
            }
        });
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        IsLoading = true;
        try
        {
            var from = DateTimeOffset.Now - TimeSpan.FromHours(24);
            var to = DateTimeOffset.Now;
            var records = await _repository.GetAlertsAsync(from, to).ConfigureAwait(true);

            Alerts.Clear();
            foreach (var r in records)
            {
                Alerts.Add(new AlertRowViewModel(r));
            }

            StatusMessage = $"{records.Count} alertes sur 24h.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Échec chargement : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Alerts.Clear();
        StatusMessage = "Liste affichée vidée (historique préservé).";
    }

    public void Dispose() => _engine.AlertRaised -= OnAlertRaised;
}
