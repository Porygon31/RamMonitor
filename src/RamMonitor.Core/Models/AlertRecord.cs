namespace RamMonitor.Core.Models;

/// <summary>
/// Alerte effectivement déclenchée. Persistée en SQLite et affichée
/// dans le centre des alertes / notifications toast Windows.
/// </summary>
public sealed record AlertRecord
{
    /// <summary>Date du déclenchement.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Identifiant de la <see cref="AlertRule"/> à l'origine.</summary>
    public required string RuleId { get; init; }

    /// <summary>Type de règle (dupliqué ici pour faciliter les requêtes SQLite).</summary>
    public required AlertRuleKind Kind { get; init; }

    /// <summary>Gravité de cette alerte.</summary>
    public required AlertSeverity Severity { get; init; }

    /// <summary>Titre court (apparaît dans le toast Windows).</summary>
    public required string Title { get; init; }

    /// <summary>Message détaillé avec la valeur mesurée et le seuil.</summary>
    public required string Message { get; init; }

    /// <summary>PID associé pour les alertes par process (sinon <c>null</c>).</summary>
    public int? RelatedProcessId { get; init; }

    /// <summary>Nom du process associé (cache pour éviter une nouvelle lookup).</summary>
    public string? RelatedProcessName { get; init; }

    /// <summary>Valeur mesurée qui a déclenché l'alerte (selon <see cref="Kind"/>).</summary>
    public double? MeasuredValue { get; init; }

    /// <summary>Seuil configuré au moment du déclenchement.</summary>
    public double? ThresholdValue { get; init; }
}
