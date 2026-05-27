using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Infrastructure.Interop;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace RamMonitor.Infrastructure.System;

/// <summary>
/// Détection de l'élévation et relance en mode élevé.
/// Pattern Process Explorer : démarrage en asInvoker + bouton "Run as Administrator"
/// qui spawn une nouvelle instance avec verbe "runas" → prompt UAC.
/// </summary>
public sealed class Win32ElevationService : IElevationService
{
    private readonly ILogger<Win32ElevationService> _logger;
    private readonly Lazy<bool> _isElevated;

    public Win32ElevationService(ILogger<Win32ElevationService>? logger = null)
    {
        _logger = logger ?? NullLogger<Win32ElevationService>.Instance;
        _isElevated = new Lazy<bool>(DetectElevation);
    }

    public bool IsCurrentProcessElevated => _isElevated.Value;

    public bool TryRestartElevated(out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            // Le chemin de l'exécutable .NET single-file est dans Environment.ProcessPath (préféré à Assembly.Location).
            var exe = Environment.ProcessPath
                      ?? Process.GetCurrentProcess().MainModule?.FileName
                      ?? Assembly.GetEntryAssembly()?.Location
                      ?? throw new InvalidOperationException("Unable to locate the current executable.");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true, // requis pour Verb="runas"
                Verb = "runas",         // déclenche le prompt UAC
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            // L'utilisateur a refusé UAC, ou le chemin de l'exe est invalide.
            errorMessage = ex.Message;
            _logger.LogWarning(ex, "Elevated restart failed (likely user cancelled UAC).");
            return false;
        }
    }

    private bool DetectElevation()
    {
        if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                NativeConstants.TOKEN_QUERY, out var token))
        {
            return false;
        }

        using (token)
        {
            // TokenIsElevated == 0 si standard, != 0 si élevé (LUA off ou admin réel).
            if (!NativeMethods.GetTokenInformation(token, NativeConstants.TokenElevation,
                    out var elevation,
                    (uint)Marshal.SizeOf<NativeStructs.TOKEN_ELEVATION>(), out _))
            {
                return false;
            }

            return elevation.TokenIsElevated != 0;
        }
    }
}
