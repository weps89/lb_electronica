using Microsoft.Data.Sqlite;

namespace LBElectronica.Server.Services;

public class SqlMigrationService(IConfiguration config, ILogger<SqlMigrationService> logger)
{
    public async Task RunAsync()
    {
        var connStr = config.GetConnectionString("Default") ?? "Data Source=lb_electronica.db";
        await using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        var createTable = conn.CreateCommand();
        createTable.CommandText = "CREATE TABLE IF NOT EXISTS __app_migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL);";
        await createTable.ExecuteNonQueryAsync();

        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (!Directory.Exists(migrationsDir))
            migrationsDir = Path.Combine(Directory.GetCurrentDirectory(), "Migrations");

        if (!Directory.Exists(migrationsDir)) return;

        var files = Directory.GetFiles(migrationsDir, "*.sql").OrderBy(x => x).ToList();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(1) FROM __app_migrations WHERE name = $name";
            check.Parameters.AddWithValue("$name", name);
            var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
            if (exists) continue;

            var sql = await File.ReadAllTextAsync(file);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();

            var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO __app_migrations(name, applied_at) VALUES($name, $at)";
            ins.Parameters.AddWithValue("$name", name);
            ins.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
            await ins.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            logger.LogInformation("Applied SQL migration {Migration}", name);
        }

        var prag = conn.CreateCommand();
        prag.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        await prag.ExecuteNonQueryAsync();
    }
}
