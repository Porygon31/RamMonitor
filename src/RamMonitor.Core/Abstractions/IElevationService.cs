namespace RamMonitor.Core.Abstractions;

/// <summary>
/// Détection de l'élévation du process courant et relance élevée
/// (pattern Process Explorer : un bouton lance une nouvelle instance avec UAC).
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// <c>true</c> si le token courant est élevé (admin réel ou LUA off).
    /// Évalué une fois et caché — pas de coût si appelé plusieurs fois.
    /// </summary>
    bool IsCurrentProcessElevated { get; }

    /// <summary>
    /// Tente de relancer l'application en mode Administrator. Retourne <c>true</c>
    /// si la nouvelle instance a été lancée avec succès. <c>false</c> si l'utilisateur
    /// a refusé UAC (errorMessage renseigné dans ce cas).
    /// </summary>
    bool TryRestartElevated(out string? errorMessage);
}
