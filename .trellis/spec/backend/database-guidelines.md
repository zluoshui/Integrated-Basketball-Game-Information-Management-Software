# Database Guidelines

> Database patterns and conventions for this project.

---

## Overview

<!--
Document your project's database conventions here.

Questions to answer:
- What ORM/query library do you use?
- How are migrations managed?
- What are the naming conventions for tables/columns?
- How do you handle transactions?
-->

The desktop app uses SQLite as its primary local store. The database file lives at `%AppData%\BasketballManager\basketball.db`; imported photos live under `%AppData%\BasketballManager\photos\`.

## Scenario: Local SQLite Store

### 1. Scope / Trigger
- Trigger: any change to persisted app data, schema migrations, backup/restore, or legacy import.

### 2. Signatures
- Database path: `DataStore.DataPath`
- Photo directory: `DataStore.PhotoDirectory`
- Load contract: `AppData Load()`
- Save contract: `void Save(AppData data)`
- Backup contract: `string Backup()`
- Legacy import: `basketball-data.json` imports only when SQLite is empty.

### 3. Contracts
- `schema_info(version INTEGER NOT NULL)` stores the schema version.
- Schema version `1` owns the initial `players`, `player_field_definitions`, `player_field_values`, `teams`, `matches`, `match_rosters`, and `match_events` tables.
- Schema version `2` adds structured team references for players/matches, team status, and match scheduling fields.
- Schema version `3` adds persisted match status for clock control (`NotStarted`, `Running`, `Paused`, `Interval`, `Finished`).
- IDs are stored as `TEXT` GUID strings.
- Dates are stored as round-trip UTC text via `DateTime.ToString("O")`.
- Booleans are stored as `INTEGER` values `0` or `1`.
- Photo paths stored in the database must be relative to the app data directory.
- Clock state must persist `current_period`, `remaining_seconds`, `status`, `is_clock_running`, and `last_clock_update_utc`.
- Match event notes are part of the event log contract and must survive save/load round trips.

### 4. Validation & Error Matrix
- Missing database -> create the latest schema version.
- Existing version lower than current -> run `MigrateDatabase()` and update `schema_info`.
- Empty SQLite plus existing JSON -> import JSON then save into SQLite.
- Duplicate non-empty student number -> reject before save.
- Database read/write failure -> throw `InvalidOperationException` with the database path and original message.
- Missing photo file -> UI shows a placeholder message, not an exception.

### 5. Good/Base/Bad Cases
- Good: app loads SQLite, edits a player, saves transactionally, reopens with the same data.
- Base: app opens with no data and creates an empty schema.
- Bad: app continues to use JSON as the primary database after schema versioning exists.

### 6. Tests Required
- `dotnet build .\BasketballManager.sln`
- `dotnet run --project .\src\BasketballManager\BasketballManager.csproj -- --self-check`
- `dotnet list .\src\BasketballManager\BasketballManager.csproj package --vulnerable --include-transitive`

### 7. Wrong vs Correct

Wrong:

```csharp
File.WriteAllText(DataPath, JsonSerializer.Serialize(data));
```

Correct:

```csharp
using var transaction = connection.BeginTransaction();
// write related tables
transaction.Commit();
```

---

## Query Patterns

<!-- How should queries be written? Batch operations? -->

- Keep all writes for a full save inside one SQLite transaction.
- Keep foreign keys enabled for each opened connection with `PRAGMA foreign_keys = ON`.
- For now, the app loads into `AppData` and writes the full graph transactionally. Add incremental repository methods only when data size makes this too slow.

---

## Migrations

<!-- How to create and run migrations -->

- Add schema changes by incrementing `CurrentSchemaVersion`.
- Put version-to-version changes in `MigrateDatabase()`.
- Wrap each migration run in one SQLite transaction and update `schema_info` only after all DDL/data changes succeed.
- Use idempotent helpers such as `AddColumnIfMissing()` for additive migrations.
- Keep existing data migration in `EnsureDatabase`; do not drop user data.
- Preserve legacy JSON import until existing users have had a clear migration window.

---

## Naming Conventions

<!-- Table names, column names, index names -->

- Table and column names use lower snake case.
- Unique indexes use `ux_<table>_<column>`.
- Foreign key columns use `<entity>_id`.

---

## Common Mistakes

<!-- Database-related mistakes your team has made -->

- Do not hard-code absolute user paths for the database or photos.
- Do not store original photo source paths; import photos and store relative paths.
- Do not ignore NuGet vulnerability warnings on SQLite native dependencies.
- Do not physically delete players that already have match events; mark them inactive instead.
- Do not directly remove events from finished matches; later correction flows should preserve an audit trail.
