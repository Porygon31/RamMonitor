namespace RamMonitor.Core.Models;

/// <summary>
/// Niveaux de priorité CPU d'un process. Mappés sur les classes Win32
/// (<c>SetPriorityClass</c>). L'ordre va du moins prioritaire au plus prioritaire.
/// </summary>
public enum ProcessPriorityLevel
{
    /// <summary>Idle : tourne uniquement quand rien d'autre n'a besoin du CPU.</summary>
    Idle = 0,

    /// <summary>Below normal : priorité réduite.</summary>
    BelowNormal = 1,

    /// <summary>Normal : valeur par défaut.</summary>
    Normal = 2,

    /// <summary>Above normal : priorité élevée.</summary>
    AboveNormal = 3,

    /// <summary>High : priorité très élevée, peut affecter la réactivité de l'OS.</summary>
    High = 4,

    /// <summary>
    /// Realtime : priorité maximale. Peut figer la machine si le process boucle.
    /// À utiliser avec extrême précaution.
    /// </summary>
    Realtime = 5
}
