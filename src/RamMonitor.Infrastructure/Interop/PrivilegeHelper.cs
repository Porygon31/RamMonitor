using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RamMonitor.Infrastructure.Interop;

/// <summary>
/// Active / désactive des privilèges Windows sur le token du process courant.
/// Indispensable pour : dump mémoire de process système (SeDebug), purge du
/// standby cache (SeProfileSingleProcess), gestion du pagefile (SeCreatePagefile).
/// </summary>
internal sealed class PrivilegeHelper
{
    private readonly ILogger _logger;

    public PrivilegeHelper(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Tente d'activer un privilège nommé sur le token courant.
    /// Retourne <c>false</c> silencieusement si le process n'est pas élevé
    /// (et donc ne possède pas le privilège dans son token).
    /// </summary>
    public bool TryEnable(string privilegeName)
    {
        // Ouvre le token du process courant en TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY.
        if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                NativeConstants.TOKEN_ADJUST_PRIVILEGES | NativeConstants.TOKEN_QUERY,
                out var tokenHandle))
        {
            _logger.LogDebug("OpenProcessToken failed: {Error}",
                new Win32Exception(Marshal.GetLastWin32Error()).Message);
            return false;
        }

        using (tokenHandle)
        {
            // Résolution du nom de privilège en LUID local.
            if (!NativeMethods.LookupPrivilegeValue(null, privilegeName, out var luid))
            {
                _logger.LogDebug("LookupPrivilegeValue({Name}) failed.", privilegeName);
                return false;
            }

            var tp = new NativeStructs.TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new NativeStructs.LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = NativeConstants.SE_PRIVILEGE_ENABLED
                }
            };

            // AdjustTokenPrivileges retourne TRUE même si le privilège n'a pas pu être activé.
            // Il faut vérifier GetLastError() == ERROR_SUCCESS pour confirmer le succès réel.
            if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tp,
                    (uint)Marshal.SizeOf<NativeStructs.TOKEN_PRIVILEGES>(),
                    IntPtr.Zero, IntPtr.Zero))
            {
                _logger.LogDebug("AdjustTokenPrivileges failed for {Name}.", privilegeName);
                return false;
            }

            var lastError = Marshal.GetLastWin32Error();
            if (lastError != NativeConstants.ERROR_SUCCESS)
            {
                _logger.LogDebug("Privilege {Name} not held by token (error {Code}).",
                    privilegeName, lastError);
                return false;
            }
        }

        _logger.LogDebug("Privilege {Name} enabled.", privilegeName);
        return true;
    }
}

// (La classe utilitaire Marshal a été supprimée pour éviter une collision avec
// System.Runtime.InteropServices.Marshal. Tous les appelants importent désormais
// directement le namespace System.Runtime.InteropServices.)
