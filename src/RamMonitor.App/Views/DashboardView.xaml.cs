using System.IO;
using System.Windows.Controls;
using LiveChartsCore.SkiaSharpView.SKCharts;
using RamMonitor.App.ViewModels;

namespace RamMonitor.App.Views;

/// <summary>
/// Code-behind du Dashboard. Fournit au ViewModel une capture PNG du graphe à la demande
/// (pour l'export PNG depuis l'History).
/// </summary>
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DashboardViewModel vm)
        {
            vm.ChartImageCapture = CaptureChartAsPngAsync;
        }
    }

    private ValueTask<byte[]?> CaptureChartAsPngAsync()
    {
        try
        {
            return new ValueTask<byte[]?>(System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var chart = new SKCartesianChart
                {
                    Width = 1200,
                    Height = 600,
                    Series = LiveChart.Series,
                    XAxes = LiveChart.XAxes,
                    YAxes = LiveChart.YAxes,
                    Background = SkiaSharp.SKColors.Transparent
                };

                using var stream = new MemoryStream();
                chart.SaveImage(stream, SkiaSharp.SKEncodedImageFormat.Png, 90);
                return stream.ToArray();
            }));
        }
        catch
        {
            return ValueTask.FromResult<byte[]?>(null);
        }
    }
}
