namespace RamMonitor.App.Composition;

/// <summary>
/// Racine des options applicatives (mappées depuis la section "RamMonitor" du
/// appsettings.json via <c>IConfiguration.Bind</c>). Chaque sous-objet est passé
/// individuellement aux services qui en ont besoin.
/// </summary>
public sealed class AppOptions
{
    public PollingOptions Polling { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
    public AlertsOptions Alerts { get; set; } = new();
    public LeakDetectionOptions LeakDetection { get; set; } = new();
    public ExportOptions Export { get; set; } = new();
    public UiOptions Ui { get; set; } = new();
}

public sealed class PollingOptions
{
    public int FastIntervalMs { get; set; } = 1000;
    public int SlowIntervalMs { get; set; } = 5000;
    public int MinimizedIntervalMs { get; set; } = 10000;
    public bool PauseWhenMinimized { get; set; } = false;
}

public sealed class DatabaseOptions
{
    public string FileName { get; set; } = "rammonitor.db";
    public int RetentionDays { get; set; } = 7;
    public bool AutoVacuumOnStartup { get; set; } = true;
}

public sealed class AlertsOptions
{
    public double GlobalRamPercentWarning { get; set; } = 80.0;
    public double GlobalRamPercentCritical { get; set; } = 92.0;
    public double PagefilePercentWarning { get; set; } = 75.0;
    public double ProcessWorkingSetWarningMb { get; set; } = 2048;
    public bool EnableToasts { get; set; } = true;
}

public sealed class LeakDetectionOptions
{
    public bool Enabled { get; set; } = true;
    public int WindowMinutes { get; set; } = 30;
    public int MinSamples { get; set; } = 20;
    public double GrowthRateBytesPerMinuteThreshold { get; set; } = 1_048_576;
    public double MinR2 { get; set; } = 0.85;
}

public sealed class ExportOptions
{
    public string DefaultFolder { get; set; } = "exports";
}

public sealed class UiOptions
{
    public string Theme { get; set; } = "Dark";
    public int MaxProcessRows { get; set; } = 200;
    public int ChartHistorySeconds { get; set; } = 120;
}
