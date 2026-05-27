using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Toolkit.Uwp.Notifications;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Infrastructure.Notifications;

/// <summary>
/// Notifier d'alertes via Windows Notifications (toasts).
/// Le toast utilise la queue système : il survit même si l'app est minimisée.
/// La désactivation via <see cref="Enabled"/> permet à l'utilisateur de couper
/// les toasts depuis les Settings sans affecter la persistance en base.
/// </summary>
public sealed class ToastAlertNotifier : IAlertNotifier
{
    private readonly ILogger<ToastAlertNotifier> _logger;

    public ToastAlertNotifier(ILogger<ToastAlertNotifier>? logger = null)
    {
        _logger = logger ?? NullLogger<ToastAlertNotifier>.Instance;
    }

    /// <summary>
    /// Bascule globale. Modifié par les Settings de l'UI.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public ValueTask NotifyAsync(AlertRecord alert, CancellationToken cancellationToken = default)
    {
        if (!Enabled)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            // Adapter le ton du toast selon la gravité (couleur d'attribution + scénario).
            var scenario = alert.Severity switch
            {
                AlertSeverity.Critical => ToastScenario.Reminder,
                AlertSeverity.Warning  => ToastScenario.Default,
                _                       => ToastScenario.Default
            };

            new ToastContentBuilder()
                .AddText(alert.Title)
                .AddText(alert.Message)
                .AddAttributionText($"RamMonitor — {alert.Severity}")
                .SetToastScenario(scenario)
                .Show(toast =>
                {
                    // Tag/Group : permettent de remplacer une notification précédente
                    // de la même règle plutôt que d'empiler.
                    toast.Tag = alert.RuleId;
                    toast.Group = "RamMonitor";
                });
        }
        catch (Exception ex)
        {
            // Sur certains environnements (Windows Server sans UI, accounts sans profile),
            // les toasts ne sont pas disponibles : on log mais on ne casse pas le pipeline.
            _logger.LogDebug(ex, "Toast notification failed (system likely doesn't support it).");
        }

        return ValueTask.CompletedTask;
    }
}
