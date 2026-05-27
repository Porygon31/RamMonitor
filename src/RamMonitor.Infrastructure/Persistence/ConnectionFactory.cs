using Microsoft.Data.Sqlite;

namespace RamMonitor.Infrastructure.Persistence;

/// <summary>
/// Fabrique de connexions SQLite. Centralise la chaîne de connexion et applique
/// les pragmas critiques (WAL pour les écritures concurrentes, foreign_keys ON,
/// synchronous=NORMAL pour un bon compromis perf/durabilité).
/// </summary>
public sealed class ConnectionFactory
{
    private readonly string _connectionString;

    public ConnectionFactory(string databasePath)
    {
        // Cache=Shared : permet à plusieurs connexions dans le même process de partager le pool.
        // Mode=ReadWriteCreate : la base est créée si absente.
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };
        _connectionString = builder.ToString();
    }

    /// <summary>
    /// Ouvre une connexion prête à l'emploi. L'appelant est responsable du Dispose
    /// (généralement via <c>await using</c>).
    /// </summary>
    public async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Pragmas appliqués à chaque connexion (SQLite les associe à la session, pas au fichier).
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA temp_store = MEMORY;
        """;
        await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }
}
