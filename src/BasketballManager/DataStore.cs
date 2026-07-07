using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace BasketballManager;

public sealed class DataStore
{
    private const int CurrentSchemaVersion = 5;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DataStore(string? dataDirectory = null)
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("BASKETBALL_MANAGER_DATA_DIR");
        DataDirectory = dataDirectory
            ?? (string.IsNullOrWhiteSpace(overrideDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BasketballManager")
                : overrideDirectory);
    }

    public string DataDirectory { get; }
    public string DataPath => Path.Combine(DataDirectory, "basketball.db");
    public string LegacyJsonPath => Path.Combine(DataDirectory, "basketball-data.json");
    public string PhotoDirectory => Path.Combine(DataDirectory, "photos");
    public string ExportDirectorySettingsPath => Path.Combine(DataDirectory, "export-directory.txt");

    public AppData Load()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(PhotoDirectory);
        EnsureDatabase();

        try
        {
            var data = LoadFromDatabase();
            if (IsEmpty(data) && File.Exists(LegacyJsonPath))
            {
                data = LoadLegacyJson();
                Save(data);
            }

            return data;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法读取本地数据库: {DataPath}. {ex.Message}", ex);
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(PhotoDirectory);
        EnsureDatabase();

        try
        {
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, "DELETE FROM match_events");
            Execute(connection, transaction, "DELETE FROM match_rosters");
            Execute(connection, transaction, "DELETE FROM matches");
            Execute(connection, transaction, "DELETE FROM player_field_values");
            Execute(connection, transaction, "DELETE FROM player_field_definitions");
            Execute(connection, transaction, "DELETE FROM teams");
            Execute(connection, transaction, "DELETE FROM players");

            foreach (var player in data.Players)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO players (id, name, student_number, team_id, team, note, photo_path, status, created_at, updated_at)
                    VALUES ($id, $name, $student_number, $team_id, $team, $note, $photo_path, $status, $created_at, $updated_at)
                    """,
                    ("$id", player.Id.ToString()),
                    ("$name", player.Name),
                    ("$student_number", player.StudentNumber),
                    ("$team_id", ToText(player.TeamId)),
                    ("$team", player.Team),
                    ("$note", player.Note),
                    ("$photo_path", player.PhotoPath),
                    ("$status", player.Status),
                    ("$created_at", ToText(player.CreatedAt)),
                    ("$updated_at", ToText(player.UpdatedAt)));
            }

            foreach (var field in data.PlayerFields)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO player_field_definitions (id, name, field_type, is_required, display_order)
                    VALUES ($id, $name, $field_type, $is_required, $display_order)
                    """,
                    ("$id", field.Id.ToString()),
                    ("$name", field.Name),
                    ("$field_type", field.FieldType),
                    ("$is_required", field.IsRequired ? 1 : 0),
                    ("$display_order", field.DisplayOrder));
            }

            foreach (var value in data.PlayerFieldValues)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO player_field_values (player_id, field_id, value)
                    VALUES ($player_id, $field_id, $value)
                    """,
                    ("$player_id", value.PlayerId.ToString()),
                    ("$field_id", value.FieldId.ToString()),
                    ("$value", value.Value));
            }

            foreach (var team in data.Teams)
            {
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO teams (id, name, note, status) VALUES ($id, $name, $note, $status)",
                    ("$id", team.Id.ToString()),
                    ("$name", team.Name),
                    ("$note", team.Note),
                    ("$status", team.Status));
            }

            foreach (var match in data.Matches)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO matches (id, name, home_team_id, away_team_id, home_team_name, away_team_name, scheduled_at, location, note, period_count, period_length_seconds, current_period, remaining_seconds, status, is_clock_running, last_clock_update_utc, created_at, ended_at)
                    VALUES ($id, $name, $home_team_id, $away_team_id, $home_team_name, $away_team_name, $scheduled_at, $location, $note, $period_count, $period_length_seconds, $current_period, $remaining_seconds, $status, $is_clock_running, $last_clock_update_utc, $created_at, $ended_at)
                    """,
                    ("$id", match.Id.ToString()),
                    ("$name", match.Name),
                    ("$home_team_id", ToText(match.HomeTeamId)),
                    ("$away_team_id", ToText(match.AwayTeamId)),
                    ("$home_team_name", match.HomeTeamName),
                    ("$away_team_name", match.AwayTeamName),
                    ("$scheduled_at", ToText(match.ScheduledAt)),
                    ("$location", match.Location),
                    ("$note", match.Note),
                    ("$period_count", match.PeriodCount),
                    ("$period_length_seconds", match.PeriodLengthSeconds),
                    ("$current_period", match.CurrentPeriod),
                    ("$remaining_seconds", match.RemainingSeconds),
                    ("$status", match.Status.ToString()),
                    ("$is_clock_running", match.IsClockRunning ? 1 : 0),
                    ("$last_clock_update_utc", ToText(match.LastClockUpdateUtc)),
                    ("$created_at", ToText(match.CreatedAt)),
                    ("$ended_at", ToText(match.EndedAt)));
            }

            foreach (var roster in data.Rosters)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO match_rosters (id, match_id, player_id, side, jersey_number, is_starter)
                    VALUES ($id, $match_id, $player_id, $side, $jersey_number, $is_starter)
                    """,
                    ("$id", roster.Id.ToString()),
                    ("$match_id", roster.MatchId.ToString()),
                    ("$player_id", roster.PlayerId.ToString()),
                    ("$side", roster.Side.ToString()),
                    ("$jersey_number", roster.JerseyNumber),
                    ("$is_starter", roster.IsStarter ? 1 : 0));
            }

            foreach (var item in data.Events)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO match_events (id, match_id, player_id, side, kind, points, period, clock_seconds_remaining, note, is_voided, voided_at, void_reason, voided_by, created_at)
                    VALUES ($id, $match_id, $player_id, $side, $kind, $points, $period, $clock_seconds_remaining, $note, $is_voided, $voided_at, $void_reason, $voided_by, $created_at)
                    """,
                    ("$id", item.Id.ToString()),
                    ("$match_id", item.MatchId.ToString()),
                    ("$player_id", ToText(item.PlayerId)),
                    ("$side", item.Side.ToString()),
                    ("$kind", item.Kind.ToString()),
                    ("$points", item.Points),
                    ("$period", item.Period),
                    ("$clock_seconds_remaining", item.ClockSecondsRemaining),
                    ("$note", item.Note),
                    ("$is_voided", item.IsVoided ? 1 : 0),
                    ("$voided_at", ToText(item.VoidedAt)),
                    ("$void_reason", item.VoidReason),
                    ("$voided_by", item.VoidedBy),
                    ("$created_at", ToText(item.CreatedAt)));
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法保存本地数据库: {DataPath}. {ex.Message}", ex);
        }
    }

    public string Backup()
    {
        Directory.CreateDirectory(DataDirectory);
        EnsureDatabase();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var backupDirectory = Path.Combine(DataDirectory, "backups", stamp);
        var suffix = 1;
        while (Directory.Exists(backupDirectory))
        {
            backupDirectory = Path.Combine(DataDirectory, "backups", $"{stamp}-{suffix}");
            suffix++;
        }
        Directory.CreateDirectory(backupDirectory);

        SqliteConnection.ClearAllPools();
        File.Copy(DataPath, Path.Combine(backupDirectory, Path.GetFileName(DataPath)), overwrite: false);
        if (Directory.Exists(PhotoDirectory))
        {
            CopyDirectory(PhotoDirectory, Path.Combine(backupDirectory, "photos"));
        }

        return backupDirectory;
    }

    public string ImportPhoto(string sourcePath)
    {
        Directory.CreateDirectory(PhotoDirectory);

        var extension = Path.GetExtension(sourcePath);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = Path.Combine(PhotoDirectory, fileName);
        File.Copy(sourcePath, targetPath, overwrite: false);
        return Path.Combine("photos", fileName);
    }

    public string ResolvePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? ""
            : Path.Combine(DataDirectory, relativePath);
    }

    public string LoadExportDirectory()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var configuredDirectory = File.Exists(ExportDirectorySettingsPath)
                ? File.ReadAllText(ExportDirectorySettingsPath).Trim()
                : "";
            var exportDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? GetDefaultExportDirectory()
                : configuredDirectory;
            Directory.CreateDirectory(exportDirectory);
            return exportDirectory;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法读取或创建导出目录设置: {ExportDirectorySettingsPath}. {ex.Message}", ex);
        }
    }

    public void SaveExportDirectory(string exportDirectory)
    {
        if (string.IsNullOrWhiteSpace(exportDirectory))
        {
            throw new InvalidOperationException("导出目录不能为空。");
        }

        try
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(exportDirectory);
            File.WriteAllText(ExportDirectorySettingsPath, exportDirectory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法保存导出目录设置: {ExportDirectorySettingsPath}. {ex.Message}", ex);
        }
    }

    private static string GetDefaultExportDirectory()
    {
        var projectRoot = FindProjectRoot(Environment.CurrentDirectory)
            ?? FindProjectRoot(AppContext.BaseDirectory)
            ?? AppContext.BaseDirectory;
        return Path.Combine(projectRoot, "exports");
    }

    private static string? FindProjectRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BasketballManager.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void EnsureDatabase()
    {
        using var connection = OpenConnection();
        Execute(connection, null, "PRAGMA foreign_keys = ON");
        Execute(connection, null, "CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL)");

        var version = GetSchemaVersion(connection);
        if (version == 0)
        {
            CreateSchema(connection);
            Execute(connection, null, "INSERT INTO schema_info (version) VALUES ($version)", ("$version", CurrentSchemaVersion));
            return;
        }

        MigrateDatabase(connection, version);
    }

    private AppData LoadFromDatabase()
    {
        using var connection = OpenConnection();
        return new AppData
        {
            SchemaVersion = GetSchemaVersion(connection),
            Players = Query(connection, "SELECT * FROM players ORDER BY name", reader => new Player
            {
                Id = ReadGuid(reader, "id"),
                Name = ReadString(reader, "name"),
                StudentNumber = ReadString(reader, "student_number"),
                TeamId = ReadNullableGuid(reader, "team_id"),
                Team = ReadString(reader, "team"),
                Note = ReadString(reader, "note"),
                PhotoPath = ReadString(reader, "photo_path"),
                Status = ReadString(reader, "status"),
                CreatedAt = ReadDate(reader, "created_at") ?? DateTime.UtcNow,
                UpdatedAt = ReadDate(reader, "updated_at") ?? DateTime.UtcNow
            }),
            PlayerFields = Query(connection, "SELECT * FROM player_field_definitions ORDER BY display_order, name", reader => new PlayerFieldDefinition
            {
                Id = ReadGuid(reader, "id"),
                Name = ReadString(reader, "name"),
                FieldType = ReadString(reader, "field_type"),
                IsRequired = ReadBool(reader, "is_required"),
                DisplayOrder = ReadInt(reader, "display_order")
            }),
            PlayerFieldValues = Query(connection, "SELECT * FROM player_field_values", reader => new PlayerFieldValue
            {
                PlayerId = ReadGuid(reader, "player_id"),
                FieldId = ReadGuid(reader, "field_id"),
                Value = ReadString(reader, "value")
            }),
            Teams = Query(connection, "SELECT * FROM teams ORDER BY name", reader => new Team
            {
                Id = ReadGuid(reader, "id"),
                Name = ReadString(reader, "name"),
                Note = ReadString(reader, "note"),
                Status = ReadString(reader, "status")
            }),
            Matches = Query(connection, "SELECT * FROM matches ORDER BY created_at", reader => new Match
            {
                Id = ReadGuid(reader, "id"),
                Name = ReadString(reader, "name"),
                HomeTeamId = ReadNullableGuid(reader, "home_team_id"),
                AwayTeamId = ReadNullableGuid(reader, "away_team_id"),
                HomeTeamName = ReadString(reader, "home_team_name"),
                AwayTeamName = ReadString(reader, "away_team_name"),
                ScheduledAt = ReadDate(reader, "scheduled_at"),
                Location = ReadString(reader, "location"),
                Note = ReadString(reader, "note"),
                PeriodCount = ReadInt(reader, "period_count"),
                PeriodLengthSeconds = ReadInt(reader, "period_length_seconds"),
                CurrentPeriod = ReadInt(reader, "current_period"),
                RemainingSeconds = ReadInt(reader, "remaining_seconds"),
                Status = ReadEnum<MatchStatus>(reader, "status"),
                IsClockRunning = ReadBool(reader, "is_clock_running"),
                LastClockUpdateUtc = ReadDate(reader, "last_clock_update_utc"),
                CreatedAt = ReadDate(reader, "created_at") ?? DateTime.UtcNow,
                EndedAt = ReadDate(reader, "ended_at")
            }),
            Rosters = Query(connection, "SELECT * FROM match_rosters", reader => new MatchRoster
            {
                Id = ReadGuid(reader, "id"),
                MatchId = ReadGuid(reader, "match_id"),
                PlayerId = ReadGuid(reader, "player_id"),
                Side = ReadEnum<TeamSide>(reader, "side"),
                JerseyNumber = ReadString(reader, "jersey_number"),
                IsStarter = ReadBool(reader, "is_starter")
            }),
            Events = Query(connection, "SELECT * FROM match_events ORDER BY created_at", reader => new MatchEvent
            {
                Id = ReadGuid(reader, "id"),
                MatchId = ReadGuid(reader, "match_id"),
                PlayerId = ReadNullableGuid(reader, "player_id"),
                Side = ReadEnum<TeamSide>(reader, "side"),
                Kind = ReadEnum<MatchEventKind>(reader, "kind"),
                Points = ReadInt(reader, "points"),
                Period = ReadInt(reader, "period"),
                ClockSecondsRemaining = ReadInt(reader, "clock_seconds_remaining"),
                Note = ReadString(reader, "note"),
                IsVoided = ReadBool(reader, "is_voided"),
                VoidedAt = ReadDate(reader, "voided_at"),
                VoidReason = ReadString(reader, "void_reason"),
                VoidedBy = ReadString(reader, "voided_by"),
                CreatedAt = ReadDate(reader, "created_at") ?? DateTime.UtcNow
            })
        };
    }

    private void CreateSchema(SqliteConnection connection)
    {
        Execute(connection, null, """
            CREATE TABLE players (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                student_number TEXT NOT NULL DEFAULT '',
                team_id TEXT NULL,
                team TEXT NOT NULL DEFAULT '',
                note TEXT NOT NULL DEFAULT '',
                photo_path TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT '在队',
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            )
            """);
        Execute(connection, null, "CREATE UNIQUE INDEX IF NOT EXISTS ux_players_student_number ON players(student_number) WHERE student_number <> ''");
        Execute(connection, null, """
            CREATE TABLE player_field_definitions (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                field_type TEXT NOT NULL DEFAULT 'Text',
                is_required INTEGER NOT NULL DEFAULT 0,
                display_order INTEGER NOT NULL DEFAULT 0
            )
            """);
        Execute(connection, null, """
            CREATE TABLE player_field_values (
                player_id TEXT NOT NULL,
                field_id TEXT NOT NULL,
                value TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (player_id, field_id),
                FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
                FOREIGN KEY (field_id) REFERENCES player_field_definitions(id) ON DELETE CASCADE
            )
            """);
        Execute(connection, null, "CREATE TABLE teams (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, note TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT '启用')");
        Execute(connection, null, """
            CREATE TABLE matches (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                home_team_id TEXT NULL,
                away_team_id TEXT NULL,
                home_team_name TEXT NOT NULL,
                away_team_name TEXT NOT NULL,
                scheduled_at TEXT NULL,
                location TEXT NOT NULL DEFAULT '',
                note TEXT NOT NULL DEFAULT '',
                period_count INTEGER NOT NULL DEFAULT 4,
                period_length_seconds INTEGER NOT NULL,
                current_period INTEGER NOT NULL,
                remaining_seconds INTEGER NOT NULL,
                status TEXT NOT NULL DEFAULT 'NotStarted',
                is_clock_running INTEGER NOT NULL,
                last_clock_update_utc TEXT NULL,
                created_at TEXT NOT NULL,
                ended_at TEXT NULL
            )
            """);
        Execute(connection, null, """
            CREATE TABLE match_rosters (
                id TEXT PRIMARY KEY,
                match_id TEXT NOT NULL,
                player_id TEXT NOT NULL,
                side TEXT NOT NULL,
                jersey_number TEXT NOT NULL DEFAULT '',
                is_starter INTEGER NOT NULL DEFAULT 0,
                UNIQUE (match_id, player_id),
                FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE,
                FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE
            )
            """);
        Execute(connection, null, """
            CREATE TABLE match_events (
                id TEXT PRIMARY KEY,
                match_id TEXT NOT NULL,
                player_id TEXT NULL,
                side TEXT NOT NULL,
                kind TEXT NOT NULL,
                points INTEGER NOT NULL DEFAULT 0,
                period INTEGER NOT NULL,
                clock_seconds_remaining INTEGER NOT NULL,
                note TEXT NOT NULL DEFAULT '',
                is_voided INTEGER NOT NULL DEFAULT 0,
                voided_at TEXT NULL,
                void_reason TEXT NOT NULL DEFAULT '',
                voided_by TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL,
                FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE,
                FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE
            )
            """);
    }

    private void MigrateDatabase(SqliteConnection connection, int version)
    {
        if (version >= CurrentSchemaVersion)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        if (version < 2)
        {
            AddColumnIfMissing(connection, transaction, "players", "team_id", "TEXT NULL");
            AddColumnIfMissing(connection, transaction, "teams", "status", "TEXT NOT NULL DEFAULT '启用'");
            AddColumnIfMissing(connection, transaction, "matches", "home_team_id", "TEXT NULL");
            AddColumnIfMissing(connection, transaction, "matches", "away_team_id", "TEXT NULL");
            AddColumnIfMissing(connection, transaction, "matches", "scheduled_at", "TEXT NULL");
            AddColumnIfMissing(connection, transaction, "matches", "location", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(connection, transaction, "matches", "note", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(connection, transaction, "matches", "period_count", "INTEGER NOT NULL DEFAULT 4");
            Execute(connection, transaction, "UPDATE schema_info SET version = 2");
        }

        if (version < 3)
        {
            AddColumnIfMissing(connection, transaction, "matches", "status", "TEXT NOT NULL DEFAULT 'NotStarted'");
            Execute(connection, transaction, "UPDATE schema_info SET version = 3");
        }

        if (version < 4)
        {
            AddColumnIfMissing(connection, transaction, "match_events", "is_voided", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(connection, transaction, "match_events", "voided_at", "TEXT NULL");
            AddColumnIfMissing(connection, transaction, "match_events", "void_reason", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfMissing(connection, transaction, "match_events", "voided_by", "TEXT NOT NULL DEFAULT ''");
            Execute(connection, transaction, "UPDATE schema_info SET version = 4");
        }

        if (version < 5)
        {
            Execute(connection, transaction, """
                CREATE TABLE match_events_v5 (
                    id TEXT PRIMARY KEY,
                    match_id TEXT NOT NULL,
                    player_id TEXT NULL,
                    side TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    points INTEGER NOT NULL DEFAULT 0,
                    period INTEGER NOT NULL,
                    clock_seconds_remaining INTEGER NOT NULL,
                    note TEXT NOT NULL DEFAULT '',
                    is_voided INTEGER NOT NULL DEFAULT 0,
                    voided_at TEXT NULL,
                    void_reason TEXT NOT NULL DEFAULT '',
                    voided_by TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL,
                    FOREIGN KEY (match_id) REFERENCES matches(id) ON DELETE CASCADE,
                    FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE
                )
                """);
            Execute(connection, transaction, """
                INSERT INTO match_events_v5 (
                    id, match_id, player_id, side, kind, points, period, clock_seconds_remaining,
                    note, is_voided, voided_at, void_reason, voided_by, created_at
                )
                SELECT
                    id, match_id, player_id, side, kind, points, period, clock_seconds_remaining,
                    note, is_voided, voided_at, void_reason, voided_by, created_at
                FROM match_events
                """);
            Execute(connection, transaction, "DROP TABLE match_events");
            Execute(connection, transaction, "ALTER TABLE match_events_v5 RENAME TO match_events");
            Execute(connection, transaction, "UPDATE schema_info SET version = 5");
        }

        transaction.Commit();
    }

    private static void AddColumnIfMissing(SqliteConnection connection, SqliteTransaction transaction, string tableName, string columnName, string definition)
    {
        var columnExists = false;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({tableName})";
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (ReadString(reader, "name").Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            Execute(connection, transaction, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}");
        }
    }

    private AppData LoadLegacyJson()
    {
        var json = File.ReadAllText(LegacyJsonPath);
        return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
    }

    private SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(DataDirectory);
        var connection = new SqliteConnection($"Data Source={DataPath}");
        connection.Open();
        Execute(connection, null, "PRAGMA foreign_keys = ON");
        return connection;
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_info LIMIT 1";
        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    private static List<T> Query<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var results = new List<T>();
        while (reader.Read())
        {
            results.Add(map(reader));
        }

        return results;
    }

    private static string ToText(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static string? ToText(DateTime? value) => value is null ? null : ToText(value.Value);
    private static string? ToText(Guid? value) => value?.ToString();
    private static string ReadString(SqliteDataReader reader, string name) => reader[name] as string ?? "";
    private static int ReadInt(SqliteDataReader reader, string name) => Convert.ToInt32(reader[name], CultureInfo.InvariantCulture);
    private static bool ReadBool(SqliteDataReader reader, string name) => ReadInt(reader, name) != 0;
    private static Guid ReadGuid(SqliteDataReader reader, string name) => Guid.Parse(ReadString(reader, name));
    private static Guid? ReadNullableGuid(SqliteDataReader reader, string name)
    {
        var value = ReadString(reader, name);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static DateTime? ReadDate(SqliteDataReader reader, string name)
    {
        var value = reader[name];
        if (value is null || value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return null;
        }

        return DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static T ReadEnum<T>(SqliteDataReader reader, string name) where T : struct
    {
        return Enum.TryParse<T>(ReadString(reader, name), out var value) ? value : default;
    }

    private static bool IsEmpty(AppData data)
    {
        return data.Players.Count == 0
            && data.PlayerFields.Count == 0
            && data.PlayerFieldValues.Count == 0
            && data.Teams.Count == 0
            && data.Matches.Count == 0
            && data.Rosters.Count == 0
            && data.Events.Count == 0;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }
}
