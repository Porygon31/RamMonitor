using System.Windows;
using System.Windows.Controls;
using RamMonitor.App.ViewModels;

namespace RamMonitor.App.Views;

/// <summary>
/// Fenêtre principale. Code-behind minimal : uniquement le routage des clics
/// de la sidebar vers MainViewModel.SelectedTab.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNavClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is RadioButton rb && rb.Tag is string tag)
        {
            vm.SelectedTab = tag;
        }
    }
}
