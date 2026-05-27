using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RamMonitor.App.Views;

/// <summary>
/// Vue Process. Code-behind minimal : juste un handler qui sélectionne la ligne
/// au clic droit avant l'affichage du menu contextuel (sinon le menu agit sur
/// la sélection précédente plutôt que sur la ligne cliquée).
/// </summary>
public partial class ProcessesView : UserControl
{
    public ProcessesView()
    {
        InitializeComponent();
    }

    private void OnGridRightClick(object sender, MouseButtonEventArgs e)
    {
        // Remonte l'arbre visuel jusqu'à la DataGridRow contenant l'élément cliqué.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null and not DataGridRow)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is DataGridRow row)
        {
            // Sélectionne la ligne avant que le ContextMenu ne s'ouvre.
            row.IsSelected = true;
        }
    }
}
