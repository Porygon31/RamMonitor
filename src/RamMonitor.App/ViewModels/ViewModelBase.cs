using CommunityToolkit.Mvvm.ComponentModel;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Classe de base pour tous les ViewModels.
/// <see cref="ObservableObject"/> implémente INotifyPropertyChanged + helpers SetProperty.
/// Les ViewModels concrets utilisent les source generators
/// [ObservableProperty] et [RelayCommand].
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Exécute une action sur le thread UI. Indispensable quand le MonitoringEngine
    /// (qui tourne sur des threads de pool) met à jour des ObservableCollection
    /// bindées à des DataGrid WPF.
    /// </summary>
    protected static void RunOnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
