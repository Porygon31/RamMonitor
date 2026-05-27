namespace RamMonitor.Core.Models;

/// <summary>
/// Niveaux de gravité d'une alerte. Ordonnés du moins au plus sévère
/// pour permettre des comparaisons (<c>severity &gt;= Warning</c>).
/// </summary>
public enum AlertSeverity
{
    /// <summary>Information : pas d'action requise.</summary>
    Info = 0,

    /// <summary>Avertissement : surveiller, l'état approche d'un seuil critique.</summary>
    Warning = 1,

    /// <summary>Critique : action recommandée.</summary>
    Critical = 2
}
