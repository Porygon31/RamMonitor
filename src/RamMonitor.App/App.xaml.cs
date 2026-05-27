using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RamMonitor.App.Composition;
using RamMonitor.App.ViewModels;
using RamMonitor.App.Views;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Services;
using Serilog;

namespace RamMonitor.App;

/// <summary>
/// Point d'entrée de l'application WPF. Configure le Generic Host
/// (Serilog + DI + Configuration), initialise SQLite, démarre le MonitoringEngine
/// puis affiche la MainWindow.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Toute exception non gérée → fichier de log avant fermeture (sinon l'utilisateur
        // voit juste une fenêtre disparaître).
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // ---- Configuration (appsettings.json + variables d'environnement) ----
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "RAMMONITOR_")
            .Build();

        // ---- Serilog (lu depuis la section "Serilog" du JSON) ----
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        try
        {
            // ---- Host générique : DI + logging + lifecycle ----
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration((_, builder) =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configuration);
                })
                .ConfigureServices((_, services) => services.AddRamMonitor(configuration))
                .Build();

            await _host.StartAsync().ConfigureAwait(true);

            // ---- Initialisation infra (schéma SQLite) ----
            var repository = _host.Services.GetRequiredService<ISnapshotRepository>();
            await repository.InitializeAsync().ConfigureAwait(true);

            // Purge automatique des anciens snapshots selon la rétention configurée.
            var options = _host.Services.GetRequiredService<AppOptions>();
            await repository.PurgeOlderThanAsync(
                DateTimeOffset.Now - TimeSpan.FromDays(options.Database.RetentionDays))
                .ConfigureAwait(true);

            // ---- Règles d'alerte par défaut ----
            var alertEvaluator = _host.Services.GetRequiredService<IAlertEvaluator>();
            alertEvaluator.SetRules(BuildDefaultAlertRules(options));

            // ---- Démarrage du moteur de monitoring ----
            var engine = _host.Services.GetRequiredService<MonitoringEngine>();
            await engine.StartAsync().ConfigureAwait(true);

            // ---- Construction de la fenêtre principale ----
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            var window = new MainWindow { DataContext = mainViewModel };

            // Pause adaptative quand l'utilisateur minimise.
            window.StateChanged += (_, _) => engine.SetMinimized(window.WindowState == WindowState.Minimized);

            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup.");
            MessageBox.Show(
                $"L'application n'a pas pu démarrer : {ex.Message}\n\nConsultez logs/rammonitor-*.log pour plus de détails.",
                "RamMonitor — Erreur fatale", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                var engine = _host.Services.GetService<MonitoringEngine>();
                if (engine is not null)
                {
                    await engine.StopAsync().ConfigureAwait(false);
                }

                await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                _host.Dispose();
            }
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static IEnumerable<Core.Models.AlertRule> BuildDefaultAlertRules(AppOptions options)
    {
        // Règles préchargées au premier lancement. L'utilisateur peut les éditer depuis
        // l'écran Settings (la persistance des règles personnalisées est un TODO post-v1).
        yield return new Core.Models.AlertRule
        {
            Id = "ram.warning",
            DisplayName = "Global RAM warning",
            Kind = Core.Models.AlertRuleKind.GlobalRamPercent,
            Threshold = options.Alerts.GlobalRamPercentWarning,
            Severity = Core.Models.AlertSeverity.Warning
        };
        yield return new Core.Models.AlertRule
        {
            Id = "ram.critical",
            DisplayName = "Global RAM critical",
            Kind = Core.Models.AlertRuleKind.GlobalRamPercent,
            Threshold = options.Alerts.GlobalRamPercentCritical,
            Severity = Core.Models.AlertSeverity.Critical
        };
        yield return new Core.Models.AlertRule
        {
            Id = "pagefile.warning",
            DisplayName = "Pagefile usage warning",
            Kind = Core.Models.AlertRuleKind.PagefilePercent,
            Threshold = options.Alerts.PagefilePercentWarning,
            Severity = Core.Models.AlertSeverity.Warning
        };
        yield return new Core.Models.AlertRule
        {
            Id = "process.workingset",
            DisplayName = "Process working set warning",
            Kind = Core.Models.AlertRuleKind.ProcessWorkingSetMb,
            Threshold = options.Alerts.ProcessWorkingSetWarningMb,
            Severity = Core.Models.AlertSeverity.Warning
        };
        yield return new Core.Models.AlertRule
        {
            Id = "leak.detected",
            DisplayName = "Suspected memory leak",
            Kind = Core.Models.AlertRuleKind.LeakDetected,
            Threshold = 0.0,
            Severity = Core.Models.AlertSeverity.Warning,
            MinReNotifyInterval = TimeSpan.FromMinutes(15)
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Empêche le crash silencieux : on logge et on affiche une boîte plutôt que
        // de laisser l'utilisateur dans le noir.
        Log.Error(e.Exception, "Unhandled dispatcher exception.");
        MessageBox.Show(e.Exception.Message, "RamMonitor — Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled AppDomain exception (IsTerminating={Terminating}).", e.IsTerminating);
        }
    }
}
