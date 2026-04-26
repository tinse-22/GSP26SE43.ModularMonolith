// See https://aka.ms/new-console-template for more information
using Npgsql;

static string? LoadEnvValue(string path, string key)
{
    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            continue;
        }

        var currentKey = line[..separator].Trim();
        if (!string.Equals(currentKey, key, StringComparison.Ordinal))
        {
            continue;
        }

        return line[(separator + 1)..].Trim().Trim('"');
    }

    return null;
}

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var envPath = Path.Combine(repoRoot, ".env");
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? LoadEnvValue(envPath, "ConnectionStrings__Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings__Default not found.");
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

await using (var command = new NpgsqlCommand("SELECT current_database(), current_schema();", connection))
await using (var reader = await command.ExecuteReaderAsync())
{
    if (await reader.ReadAsync())
    {
        Console.WriteLine($"current_database={reader.GetString(0)}");
        Console.WriteLine($"current_schema={reader.GetString(1)}");
    }
}

await using (var command = new NpgsqlCommand(
    "SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;",
    connection))
await using (var reader = await command.ExecuteReaderAsync())
{
    Console.WriteLine("latest_migrations:");
    while (await reader.ReadAsync())
    {
        Console.WriteLine(reader.GetString(0));
    }
}
