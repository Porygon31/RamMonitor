namespace RamMonitor.Core.Models;

/// <summary>
/// Type de règle d'alerte évaluée par <see cref="Abstractions.IAlertEvaluator"/>.
/// </summary>
public enum AlertRuleKind
{
    /// <summary>Pourcentage de RAM physique utilisée dépasse le seuil.</summary>
    GlobalRamPercent,

    /// <summary>Pourcentage de pagefile utilisé dépasse le seuil.</summary>
    PagefilePercent,

    /// <summary>Pourcentage du commit utilisé dépasse le seuil.</summary>
    CommitPercent,

    /// <summary>Un process dépasse un seuil en MB de Working Set.</summary>
    ProcessWorkingSetMb,

    /// <summary>Une fuite mémoire a été suspectée par le détecteur.</summary>
    LeakDetected
}

/// <summary>
/// Définition déclarative d'une règle d'alerte. Les règles sont évaluées
/// à chaque tick par <see cref="Abstractions.IAlertEvaluator"/>.
/// </summary>
public sealed record AlertRule
{
    /// <summary>Identifiant unique (utilisé pour l'anti-spam et la corrélation).</summary>
    public required string Id { get; init; }

    /// <summary>Libellé affiché à l'utilisateur dans Settings.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Type de la règle (voir <see cref="AlertRuleKind"/>).</summary>
    public required AlertRuleKind Kind { get; init; }

    /// <summary>
    /// Seuil de déclenchement. Unité dépendante du <see cref="Kind"/> :
    /// pourcentage pour les règles globales, MB pour <see cref="AlertRuleKind.ProcessWorkingSetMb"/>.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>Gravité associée à un déclenchement.</summary>
    public required AlertSeverity Severity { get; init; }

    /// <summary>Permet de désactiver la règle sans la supprimer.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Intervalle minimum entre 2 déclenchements de cette règle (anti-spam).
    /// Pour les règles par process, l'intervalle est scopé par PID.
    /// </summary>
    public TimeSpan MinReNotifyInterval { get; init; } = TimeSpan.FromMinutes(5);
}
