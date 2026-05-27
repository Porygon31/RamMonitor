using RamMonitor.Core.Models;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Représentation read-only d'une alerte pour l'affichage dans le DataGrid.
/// </summary>
public sealed class AlertRowViewModel
{
    public DateTimeOffset Timestamp { get; }
    public AlertSeverity Severity { get; }
    public string SeverityLabel => Severity.ToString();
    public string Title { get; }
    public string Message { get; }
    public string Source { get; }

    public AlertRowViewModel(AlertRecord record)
    {
        Timestamp = record.Timestamp;
        Severity = record.Severity;
        Title = record.Title;
        Message = record.Message;
        Source = record.RelatedProcessId.HasValue
            ? $"{record.RuleId} (PID {record.RelatedProcessId.Value})"
            : record.RuleId;
    }
}
