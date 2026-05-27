using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RamMonitor.App.ViewModels;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Services;
using RamMonitor.Infrastructure.Collectors;
using RamMonitor.Infrastructure.Export;
using RamMonitor.Infrastructure.Notifications;
using RamMonitor.Infrastructure.Persistence;
using RamMonitor.Infrastructure.System;

namespace RamMonitor.App.Composition;

/// <summary>
/// Composition root : déclare tous les services dans le conteneur DI.
/// Appelé une seule fois depuis <c>App.xaml.cs</c> au démarrage.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddRamMonitor(this IServiceCollection services,
        IConfiguration configuration)
    {
        // ----- Options (binding fortement typé depuis appsettings.json) -----
        services.Configure<AppOptions>(configuration.GetSection("RamMonitor"));

        var options = new AppOptions();
        configuration.GetSection("RamMonitor").Bind(options);
        services.AddSingleton(options);

        // Dapper : snake_case → PascalCase auto.
        SqliteSnapshotRepository.ConfigureDapper();

        // ----- Persistence -----
        var dbPath = ResolveDatabasePath(options.Database.FileName);
        services.AddSingleton(new ConnectionFactory(dbPath));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ISnapshotRepository>(sp => new SqliteSnapshotRepository(
            sp.GetRequiredService<ConnectionFactory>(),
            sp.GetRequiredService<DatabaseInitializer>(),
            options.Database.AutoVacuumOnStartup,
            sp.GetRequiredService<ILogger<SqliteSnapshotRepository>>()));

        // ----- Collectors & services Win32 -----
        services.AddSingleton<IMemoryCollector, Win32MemoryCollector>();
        services.AddSingleton<IProcessCollector, Win32ProcessCollector>();
        services.AddSingleton<IPagefileService, Win32PagefileService>();
        services.AddSingleton<IStandbyCacheService, Win32StandbyCacheService>();
        services.AddSingleton<IProcessActionService, Win32ProcessActionService>();
        services.AddSingleton<IMemoryDumpService, Win32MemoryDumpService>();
        services.AddSingleton<IElevationService, Win32ElevationService>();

        // ----- Notifications -----
        services.AddSingleton<ToastAlertNotifier>(sp =>
        {
            var notifier = new ToastAlertNotifier(sp.GetRequiredService<ILogger<ToastAlertNotifier>>())
            {
                Enabled = options.Alerts.EnableToasts
            };
            return notifier;
        });
        services.AddSingleton<IAlertNotifier>(sp => sp.GetRequiredService<ToastAlertNotifier>());

        // ----- Core services -----
        services.AddSingleton(new LeakDetectorOptions
        {
            Window = TimeSpan.FromMinutes(options.LeakDetection.WindowMinutes),
            MinSamples = options.LeakDetection.MinSamples,
            GrowthRateBytesPerMinuteThreshold = options.LeakDetection.GrowthRateBytesPerMinuteThreshold,
            MinR2 = options.LeakDetection.MinR2
        });
        services.AddSingleton<ILeakDetector, LeakDetector>();

        services.AddSingleton<IAlertEvaluator, AlertEvaluator>();
        services.AddSingleton<ISystemSnapshotService, SystemSnapshotService>();

        services.AddSingleton(new MonitoringEngineOptions
        {
            FastIntervalMs = options.Polling.FastIntervalMs,
            SlowIntervalMs = options.Polling.SlowIntervalMs,
            MinimizedIntervalMs = options.Polling.MinimizedIntervalMs,
            PauseWhenMinimized = options.Polling.PauseWhenMinimized
        });
        services.AddSingleton<MonitoringEngine>();

        // ----- Exporters -----
        services.AddSingleton<IExporter, CsvExporter>();
        services.AddSingleton<IExporter, JsonExporter>();
        services.AddSingleton<IExporter, HtmlReportExporter>();
        // PngExporter est registré séparément avec un provider d'image fourni par la UI.
        services.AddSingleton(sp =>
        {
            // Le provider est rebranché à runtime par le DashboardViewModel quand le chart est rendu.
            return new PngExporter(ChartImageProviderRegistry.Provider);
        });

        // ----- ViewModels (Transient pour MainViewModel, Singleton pour les sous-VMs partagés) -----
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ProcessesViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<AlertsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }

    /// <summary>
    /// Place le fichier .db à côté du binaire (en LocalAppData en cas de problème de droits).
    /// </summary>
    private static string ResolveDatabasePath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var preferred = Path.Combine(baseDir, fileName);

        try
        {
            // Test d'écriture sur le dossier du binaire (Program Files ne sera pas writable).
            var probe = Path.Combine(baseDir, ".write_probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return preferred;
        }
        catch
        {
            // Fallback : %LOCALAPPDATA%\RamMonitor\
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RamMonitor");
            Directory.CreateDirectory(local);
            return Path.Combine(local, fileName);
        }
    }
}

/// <summary>
/// Indirection pour permettre à la UI de fournir un <see cref="ChartImageProvider"/>
/// après que le conteneur DI a été construit (le chart n'existe qu'après le premier rendu WPF).
/// </summary>
internal static class ChartImageProviderRegistry
{
    public static ChartImageProvider? Provider { get; set; }
}
