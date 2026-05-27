using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RamMonitor.App.Composition;
using RamMonitor.Core.Abstractions;
using RamMonitor.Infrastructure.Notifications;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Paramétrage de l'application.
/// Les changements sont propagés à l'instance singleton de <see cref="AppOptions"/>
/// et au <see cref="ToastAlertNotifier"/> en direct.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppOptions _options;
    private readonly ToastAlertNotifier _toastNotifier;
    private readonly ISnapshotRepository _repository;
    private readonly IStandbyCacheService _standbyService;
    private readonly IElevationService _elevationService;

    [ObservableProperty] private int _fastIntervalMs;
    [ObservableProperty] private int _slowIntervalMs;
    [ObservableProperty] private bool _pauseWhenMinimized;
    [ObservableProperty] private double _globalRamPercentWarning;
    [ObservableProperty] private double _globalRamPercentCritical;
    [ObservableProperty] private double _pagefilePercentWarning;
    [ObservableProperty] private double _processWorkingSetWarningMb;
    [ObservableProperty] private bool _enableToasts;
    [ObservableProperty] private int _retentionDays;
    [ObservableProperty] private string? _statusMessage;

    public bool IsElevated => _elevationService.IsCurrentProcessElevated;

    public SettingsViewModel(
        AppOptions options,
        ToastAlertNotifier toastNotifier,
        ISnapshotRepository repository,
        IStandbyCacheService standbyService,
        IElevationService elevationService)
    {
        _options = options;
        _toastNotifier = toastNotifier;
        _repository = repository;
        _standbyService = standbyService;
        _elevationService = elevationService;

        _fastIntervalMs = options.Polling.FastIntervalMs;
        _slowIntervalMs = options.Polling.SlowIntervalMs;
        _pauseWhenMinimized = options.Polling.PauseWhenMinimized;
        _globalRamPercentWarning = options.Alerts.GlobalRamPercentWarning;
        _globalRamPercentCritical = options.Alerts.GlobalRamPercentCritical;
        _pagefilePercentWarning = options.Alerts.PagefilePercentWarning;
        _processWorkingSetWarningMb = options.Alerts.ProcessWorkingSetWarningMb;
        _enableToasts = options.Alerts.EnableToasts;
        _retentionDays = options.Database.RetentionDays;
    }

    [RelayCommand]
    private void Apply()
    {
        _options.Polling.FastIntervalMs = FastIntervalMs;
        _options.Polling.SlowIntervalMs = SlowIntervalMs;
        _options.Polling.PauseWhenMinimized = PauseWhenMinimized;
        _options.Alerts.GlobalRamPercentWarning = GlobalRamPercentWarning;
        _options.Alerts.GlobalRamPercentCritical = GlobalRamPercentCritical;
        _options.Alerts.PagefilePercentWarning = PagefilePercentWarning;
        _options.Alerts.ProcessWorkingSetWarningMb = ProcessWorkingSetWarningMb;
        _options.Alerts.EnableToasts = EnableToasts;
        _options.Database.RetentionDays = RetentionDays;

        _toastNotifier.Enabled = EnableToasts;

        StatusMessage = "Paramètres appliqués (les fréquences de polling prennent effet au prochain redémarrage).";
    }

    [RelayCommand]
    private async Task PurgeOldDataAsync()
    {
        var threshold = DateTimeOffset.Now - TimeSpan.FromDays(RetentionDays);
        await _repository.PurgeOlderThanAsync(threshold).ConfigureAwait(true);
        StatusMessage = $"Données antérieures à {threshold:yyyy-MM-dd HH:mm} purgées.";
    }

    [RelayCommand(CanExecute = nameof(IsElevated))]
    private async Task ClearStandbyListAsync()
    {
        var ok = await _standbyService.ClearStandbyListAsync().ConfigureAwait(true);
        StatusMessage = ok ? "Standby list vidée." : "Échec (privilèges insuffisants).";
    }

    [RelayCommand(CanExecute = nameof(IsElevated))]
    private async Task ClearModifiedListAsync()
    {
        var ok = await _standbyService.ClearModifiedPageListAsync().ConfigureAwait(true);
        StatusMessage = ok ? "Modified page list flushée." : "Échec (privilèges insuffisants).";
    }

    [RelayCommand(CanExecute = nameof(IsElevated))]
    private async Task ClearWorkingSetsAsync()
    {
        var ok = await _standbyService.ClearWorkingSetsAsync().ConfigureAwait(true);
        StatusMessage = ok ? "Working sets de tous les process vidés." : "Échec (privilèges insuffisants).";
    }
}
