using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RamMonitor.Infrastructure.Persistence;

/// <summary>
/// Initialise le schéma SQLite : tables memory_snapshots, process_snapshots,
/// alerts + indexes nécessaires aux requêtes time-series et par PID.
/// Toutes les instructions sont idempotentes (IF NOT EXISTS) — peut être appelé à chaque démarrage.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(ConnectionFactory connectionFactory,
        ILogger<DatabaseInitializer>? logger = null)
    {
        _connectionFactory = connectionFactory;
        _logger = logger ?? NullLogger<DatabaseInitializer>.Instance;
    }

    public async ValueTask InitializeAsync(bool autoVacuum, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Schéma. Notes :
        // - timestamp stocké en TEXT ISO-8601 (lexicographiquement triable, lisible à l'œil nu).
        // - Toutes les tailles en INTEGER (ulong dépasse int.MaxValue mais SQLite INTEGER = int64).
        //   On reste sous 2^63 = 9.2 EiB, largement suffisant.
        const string schema = """
            CREATE TABLE IF NOT EXISTS memory_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL,
                total_phys INTEGER NOT NULL,
                avail_phys INTEGER NOT NULL,
                used_phys INTEGER NOT NULL,
                commit_total INTEGER NOT NULL,
                commit_limit INTEGER NOT NULL,
                pagefile_total INTEGER NOT NULL,
                pagefile_avail INTEGER NOT NULL,
                kernel_paged INTEGER NOT NULL,
                kernel_nonpaged INTEGER NOT NULL,
                system_cache INTEGER NOT NULL,
                standby_cache INTEGER NOT NULL,
                modified_page_list INTEGER NOT NULL,
                free_zero_list INTEGER NOT NULL,
                hardware_reserved INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_memory_snapshots_ts ON memory_snapshots(ts);

            CREATE TABLE IF NOT EXISTS process_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL,
                pid INTEGER NOT NULL,
                name TEXT NOT NULL,
                working_set INTEGER NOT NULL,
                private_bytes INTEGER NOT NULL,
                virtual_size INTEGER NOT NULL,
                paged_pool INTEGER NOT NULL,
                non_paged_pool INTEGER NOT NULL,
                pagefile_bytes INTEGER NOT NULL,
                handle_count INTEGER NOT NULL,
                thread_count INTEGER NOT NULL,
                executable_path TEXT NULL,
                user_name TEXT NULL,
                is_elevated INTEGER NOT NULL DEFAULT 0,
                is_system INTEGER NOT NULL DEFAULT 0
            );

            -- Index composite : la requête principale est "historique d'un PID donné dans une fenêtre temps".
            CREATE INDEX IF NOT EXISTS ix_process_snapshots_pid_ts ON process_snapshots(pid, ts);
            CREATE INDEX IF NOT EXISTS ix_process_snapshots_ts ON process_snapshots(ts);

            CREATE TABLE IF NOT EXISTS alerts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL,
                rule_id TEXT NOT NULL,
                kind INTEGER NOT NULL,
                severity INTEGER NOT NULL,
                title TEXT NOT NULL,
                message TEXT NOT NULL,
                related_pid INTEGER NULL,
                related_process_name TEXT NULL,
                measured_value REAL NULL,
                threshold_value REAL NULL
            );

            CREATE INDEX IF NOT EXISTS ix_alerts_ts ON alerts(ts);
        """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = schema;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (autoVacuum)
        {
            // VACUUM reconstruit le fichier compactement. À faire au démarrage uniquement
            // (long et bloquant, mais courant pour les bases << 1 GB).
            await using var vacuum = connection.CreateCommand();
            vacuum.CommandText = "VACUUM;";
            try
            {
                await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VACUUM failed (non-fatal).");
            }
        }

        _logger.LogInformation("SQLite schema initialized.");
    }
}
