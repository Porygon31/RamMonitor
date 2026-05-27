using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RamMonitor.Core.Abstractions;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Coordonne la navigation entre les écrans et expose l'état d'élévation.
/// La sélection de l'onglet est exposée via <see cref="CurrentView"/> qui
/// référence l'un des sous-ViewModels enregistrés en singleton.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IElevationService _elevationService;

    public DashboardViewModel Dashboard { get; }
    public ProcessesViewModel Processes { get; }
    public HistoryViewModel History { get; }
    public AlertsViewModel Alerts { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private bool _isElevated;

    [ObservableProperty]
    private string _selectedTab = "dashboard";

    public MainViewModel(
        DashboardViewModel dashboard,
        ProcessesViewModel processes,
        HistoryViewModel history,
        AlertsViewModel alerts,
        SettingsViewModel settings,
        IElevationService elevationService)
    {
        Dashboard = dashboard;
        Processes = processes;
        History = history;
        Alerts = alerts;
        Settings = settings;

        _elevationService = elevationService;
        _isElevated = elevationService.IsCurrentProcessElevated;
        _currentView = dashboard;
    }

    partial void OnSelectedTabChanged(string value)
    {
        CurrentView = value switch
        {
            "processes" => Processes,
            "history"   => History,
            "alerts"    => Alerts,
            "settings"  => Settings,
            _           => Dashboard
        };
    }

    [RelayCommand(CanExecute = nameof(CanElevate))]
    private void Elevate()
    {
        if (_elevationService.TryRestartElevated(out var error))
        {
            System.Windows.Application.Current.Shutdown(0);
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            System.Diagnostics.Debug.WriteLine($"Elevation failed: {error}");
        }
    }

    private bool CanElevate() => !IsElevated;
}
