using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;

namespace RamMonitor.Infrastructure.Persistence;

/// <summary>
/// Repository SQLite des snapshots et alertes. Toutes les écritures bulk passent
/// par une transaction explicite (gain x100 sur SQLite en mode WAL). Les lectures
/// utilisent Dapper pour rester lisibles.
/// </summary>
public sealed class SqliteSnapshotRepository : ISnapshotRepository
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly bool _autoVacuumOnStartup;
    private readonly ILogger<SqliteSnapshotRepository> _logger;

    public SqliteSnapshotRepository(
        ConnectionFactory connectionFactory,
        DatabaseInitializer initializer,
        bool autoVacuumOnStartup,
        ILogger<SqliteSnapshotRepository>? logger = null)
    {
        _connectionFactory = connectionFactory;
        _initializer = initializer;
        _autoVacuumOnStartup = autoVacuumOnStartup;
        _logger = logger ?? NullLogger<SqliteSnapshotRepository>.Instance;
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        => _initializer.InitializeAsync(_autoVacuumOnStartup, cancellationToken);

    // ---------------------------------------------------------------------
    // ÉCRITURES
    // ---------------------------------------------------------------------

    public async ValueTask SaveMemoryAsync(MemorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO memory_snapshots
                (ts, total_phys, avail_phys, used_phys, commit_total, commit_limit,
                 pagefile_total, pagefile_avail, kernel_paged, kernel_nonpaged,
                 system_cache, standby_cache, modified_page_list, free_zero_list,
                 hardware_reserved)
            VALUES
                ($ts, $tp, $ap, $up, $ct, $cl, $pt, $pa, $kp, $knp, $sc, $sb, $mpl, $fzl, $hr);
        """;

        BindMemoryParams(cmd, snapshot);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SaveProcessesAsync(IReadOnlyList<ProcessSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Préparer la commande une fois et réutiliser les paramètres : gain massif vs N commandes.
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO process_snapshots
                (ts, pid, name, working_set, private_bytes, virtual_size,
                 paged_pool, non_paged_pool, pagefile_bytes, handle_count, thread_count,
                 executable_path, user_name, is_elevated, is_system)
            VALUES
                ($ts, $pid, $name, $ws, $pb, $vs, $pp, $npp, $pgf, $hc, $tc,
                 $exe, $usr, $elev, $sys);
        """;

        var pTs   = cmd.CreateParameter(); pTs.ParameterName = "$ts";    cmd.Parameters.Add(pTs);
        var pPid  = cmd.CreateParameter(); pPid.ParameterName = "$pid";  cmd.Parameters.Add(pPid);
        var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
        var pWs   = cmd.CreateParameter(); pWs.ParameterName = "$ws";    cmd.Parameters.Add(pWs);
        var pPb   = cmd.CreateParameter(); pPb.ParameterName = "$pb";    cmd.Parameters.Add(pPb);
        var pVs   = cmd.CreateParameter(); pVs.ParameterName = "$vs";    cmd.Parameters.Add(pVs);
        var pPp   = cmd.CreateParameter(); pPp.ParameterName = "$pp";    cmd.Parameters.Add(pPp);
        var pNpp  = cmd.CreateParameter(); pNpp.ParameterName = "$npp";  cmd.Parameters.Add(pNpp);
        var pPgf  = cmd.CreateParameter(); pPgf.ParameterName = "$pgf";  cmd.Parameters.Add(pPgf);
        var pHc   = cmd.CreateParameter(); pHc.ParameterName = "$hc";    cmd.Parameters.Add(pHc);
        var pTc   = cmd.CreateParameter(); pTc.ParameterName = "$tc";    cmd.Parameters.Add(pTc);
        var pExe  = cmd.CreateParameter(); pExe.ParameterName = "$exe";  cmd.Parameters.Add(pExe);
        var pUsr  = cmd.CreateParameter(); pUsr.ParameterName = "$usr";  cmd.Parameters.Add(pUsr);
        var pElev = cmd.CreateParameter(); pElev.ParameterName = "$elev"; cmd.Parameters.Add(pElev);
        var pSys  = cmd.CreateParameter(); pSys.ParameterName = "$sys";  cmd.Parameters.Add(pSys);

        foreach (var p in snapshots)
        {
            pTs.Value   = p.Timestamp.ToString("o", CultureInfo.InvariantCulture);
            pPid.Value  = p.ProcessId;
            pName.Value = p.Name;
            pWs.Value   = (long)p.WorkingSetBytes;
            pPb.Value   = (long)p.PrivateBytes;
            pVs.Value   = (long)p.VirtualSizeBytes;
            pPp.Value   = (long)p.PagedPoolBytes;
            pNpp.Value  = (long)p.NonPagedPoolBytes;
            pPgf.Value  = (long)p.PagefileBytes;
            pHc.Value   = (long)p.HandleCount;
            pTc.Value   = (long)p.ThreadCount;
            pExe.Value  = (object?)p.ExecutablePath ?? DBNull.Value;
            pUsr.Value  = (object?)p.UserName ?? DBNull.Value;
            pElev.Value = p.IsElevated ? 1 : 0;
            pSys.Value  = p.IsSystem ? 1 : 0;

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SaveAlertAsync(AlertRecord alert, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO alerts (ts, rule_id, kind, severity, title, message,
                                related_pid, related_process_name, measured_value, threshold_value)
            VALUES ($ts, $rid, $kind, $sev, $title, $msg, $pid, $pname, $mv, $tv);
        """;

        cmd.Parameters.AddWithValue("$ts", alert.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$rid", alert.RuleId);
        cmd.Parameters.AddWithValue("$kind", (int)alert.Kind);
        cmd.Parameters.AddWithValue("$sev", (int)alert.Severity);
        cmd.Parameters.AddWithValue("$title", alert.Title);
        cmd.Parameters.AddWithValue("$msg", alert.Message);
        cmd.Parameters.AddWithValue("$pid", (object?)alert.RelatedProcessId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pname", (object?)alert.RelatedProcessName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mv", (object?)alert.MeasuredValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tv", (object?)alert.ThresholdValue ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------
    // LECTURES (Dapper)
    // ---------------------------------------------------------------------

    public async ValueTask<IReadOnlyList<MemorySnapshot>> GetMemoryHistoryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<MemoryRow>(
            """
            SELECT ts, total_phys, avail_phys, used_phys, commit_total, commit_limit,
                   pagefile_total, pagefile_avail, kernel_paged, kernel_nonpaged,
                   system_cache, standby_cache, modified_page_list, free_zero_list, hardware_reserved
            FROM memory_snapshots
            WHERE ts >= @From AND ts <= @To
            ORDER BY ts ASC;
            """,
            new
            {
                From = from.ToString("o", CultureInfo.InvariantCulture),
                To   = to.ToString("o", CultureInfo.InvariantCulture)
            }).ConfigureAwait(false);

        return rows.Select(r => r.ToModel()).ToArray();
    }

    public async ValueTask<IReadOnlyList<ProcessSnapshot>> GetProcessHistoryAsync(int processId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<ProcessRow>(
            """
            SELECT ts, pid, name, working_set, private_bytes, virtual_size,
                   paged_pool, non_paged_pool, pagefile_bytes, handle_count, thread_count,
                   executable_path, user_name, is_elevated, is_system
            FROM process_snapshots
            WHERE pid = @Pid AND ts >= @From AND ts <= @To
            ORDER BY ts ASC;
            """,
            new
            {
                Pid  = processId,
                From = from.ToString("o", CultureInfo.InvariantCulture),
                To   = to.ToString("o", CultureInfo.InvariantCulture)
            }).ConfigureAwait(false);

        return rows.Select(r => r.ToModel()).ToArray();
    }

    public async ValueTask<IReadOnlyList<AlertRecord>> GetAlertsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<AlertRow>(
            """
            SELECT ts, rule_id, kind, severity, title, message,
                   related_pid, related_process_name, measured_value, threshold_value
            FROM alerts
            WHERE ts >= @From AND ts <= @To
            ORDER BY ts DESC;
            """,
            new
            {
                From = from.ToString("o", CultureInfo.InvariantCulture),
                To   = to.ToString("o", CultureInfo.InvariantCulture)
            }).ConfigureAwait(false);

        return rows.Select(r => r.ToModel()).ToArray();
    }

    public async ValueTask PurgeOlderThanAsync(DateTimeOffset threshold, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var iso = threshold.ToString("o", CultureInfo.InvariantCulture);

        foreach (var table in new[] { "memory_snapshots", "process_snapshots", "alerts" })
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"DELETE FROM {table} WHERE ts < $threshold;";
            cmd.Parameters.AddWithValue("$threshold", iso);
            var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Purged {Count} rows from {Table}.", deleted, table);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static void BindMemoryParams(SqliteCommand cmd, MemorySnapshot s)
    {
        cmd.Parameters.AddWithValue("$ts", s.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$tp", (long)s.TotalPhysicalBytes);
        cmd.Parameters.AddWithValue("$ap", (long)s.AvailablePhysicalBytes);
        cmd.Parameters.AddWithValue("$up", (long)s.UsedPhysicalBytes);
        cmd.Parameters.AddWithValue("$ct", (long)s.CommitTotalBytes);
        cmd.Parameters.AddWithValue("$cl", (long)s.CommitLimitBytes);
        cmd.Parameters.AddWithValue("$pt", (long)s.PageFileTotalBytes);
        cmd.Parameters.AddWithValue("$pa", (long)s.PageFileAvailableBytes);
        cmd.Parameters.AddWithValue("$kp", (long)s.KernelPagedBytes);
        cmd.Parameters.AddWithValue("$knp", (long)s.KernelNonPagedBytes);
        cmd.Parameters.AddWithValue("$sc", (long)s.SystemCacheBytes);
        cmd.Parameters.AddWithValue("$sb", (long)s.StandbyCacheBytes);
        cmd.Parameters.AddWithValue("$mpl", (long)s.ModifiedPageListBytes);
        cmd.Parameters.AddWithValue("$fzl", (long)s.FreeAndZeroPageListBytes);
        cmd.Parameters.AddWithValue("$hr", (long)s.HardwareReservedBytes);
    }

    // Lignes plates pour Dapper. snake_case → PascalCase via les noms de colonnes alias dans le SELECT.
    // (Ici Dapper matche par défaut en case-insensitive et accepte underscore via DefaultTypeMap.MatchNamesWithUnderscores).
    private sealed class MemoryRow
    {
        public string Ts { get; set; } = "";
        public long Total_phys { get; set; }
        public long Avail_phys { get; set; }
        public long Used_phys { get; set; }
        public long Commit_total { get; set; }
        public long Commit_limit { get; set; }
        public long Pagefile_total { get; set; }
        public long Pagefile_avail { get; set; }
        public long Kernel_paged { get; set; }
        public long Kernel_nonpaged { get; set; }
        public long System_cache { get; set; }
        public long Standby_cache { get; set; }
        public long Modified_page_list { get; set; }
        public long Free_zero_list { get; set; }
        public long Hardware_reserved { get; set; }

        public MemorySnapshot ToModel() => new()
        {
            Timestamp = DateTimeOffset.Parse(Ts, CultureInfo.InvariantCulture),
            TotalPhysicalBytes = (ulong)Total_phys,
            AvailablePhysicalBytes = (ulong)Avail_phys,
            UsedPhysicalBytes = (ulong)Used_phys,
            CommitTotalBytes = (ulong)Commit_total,
            CommitLimitBytes = (ulong)Commit_limit,
            PageFileTotalBytes = (ulong)Pagefile_total,
            PageFileAvailableBytes = (ulong)Pagefile_avail,
            KernelPagedBytes = (ulong)Kernel_paged,
            KernelNonPagedBytes = (ulong)Kernel_nonpaged,
            SystemCacheBytes = (ulong)System_cache,
            StandbyCacheBytes = (ulong)Standby_cache,
            ModifiedPageListBytes = (ulong)Modified_page_list,
            FreeAndZeroPageListBytes = (ulong)Free_zero_list,
            HardwareReservedBytes = (ulong)Hardware_reserved
        };
    }

    private sealed class ProcessRow
    {
        public string Ts { get; set; } = "";
        public int Pid { get; set; }
        public string Name { get; set; } = "";
        public long Working_set { get; set; }
        public long Private_bytes { get; set; }
        public long Virtual_size { get; set; }
        public long Paged_pool { get; set; }
        public long Non_paged_pool { get; set; }
        public long Pagefile_bytes { get; set; }
        public long Handle_count { get; set; }
        public long Thread_count { get; set; }
        public string? Executable_path { get; set; }
        public string? User_name { get; set; }
        public long Is_elevated { get; set; }
        public long Is_system { get; set; }

        public ProcessSnapshot ToModel() => new()
        {
            Timestamp = DateTimeOffset.Parse(Ts, CultureInfo.InvariantCulture),
            ProcessId = Pid,
            Name = Name,
            WorkingSetBytes = (ulong)Working_set,
            PrivateBytes = (ulong)Private_bytes,
            VirtualSizeBytes = (ulong)Virtual_size,
            PagedPoolBytes = (ulong)Paged_pool,
            NonPagedPoolBytes = (ulong)Non_paged_pool,
            PagefileBytes = (ulong)Pagefile_bytes,
            HandleCount = (uint)Handle_count,
            ThreadCount = (uint)Thread_count,
            ExecutablePath = Executable_path,
            UserName = User_name,
            IsElevated = Is_elevated != 0,
            IsSystem = Is_system != 0
        };
    }

    private sealed class AlertRow
    {
        public string Ts { get; set; } = "";
        public string Rule_id { get; set; } = "";
        public int Kind { get; set; }
        public int Severity { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public int? Related_pid { get; set; }
        public string? Related_process_name { get; set; }
        public double? Measured_value { get; set; }
        public double? Threshold_value { get; set; }

        public AlertRecord ToModel() => new()
        {
            Timestamp = DateTimeOffset.Parse(Ts, CultureInfo.InvariantCulture),
            RuleId = Rule_id,
            Kind = (AlertRuleKind)Kind,
            Severity = (AlertSeverity)Severity,
            Title = Title,
            Message = Message,
            RelatedProcessId = Related_pid,
            RelatedProcessName = Related_process_name,
            MeasuredValue = Measured_value,
            ThresholdValue = Threshold_value
        };
    }

    /// <summary>
    /// Configure Dapper une seule fois pour matcher les colonnes snake_case
    /// avec les propriétés Pascal_Snake (cf. <c>DefaultTypeMap.MatchNamesWithUnderscores</c>).
    /// Appelé par le composition root.
    /// </summary>
    public static void ConfigureDapper()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
