# Design

## Architecture

Create one WPF desktop project with three small layers:

- UI: `MainWindow` with tabs for players, match setup, score table, and event log.
- Domain: plain C# models for players, custom fields, teams, matches, rosters, events, clock state, and derived statistics.
- Persistence: one local store service backed by SQLite under the user's application data directory.

Legacy JSON remains only as a one-time import path when an existing JSON file is present and the SQLite database is empty.

## Data Flow

- UI edits mutate an in-memory `AppData` object through `DataStore`.
- Player and match lists bind to observable collections.
- Score table buttons append `MatchEvent` records.
- Derived score, team fouls, timeout counts, and player statistics are recomputed from event history after each event or undo.
- Clock state uses a dispatcher timer and elapsed wall-clock deltas instead of subtracting exactly one second per tick.

## Compatibility

- Target WPF on Windows x64.
- Prefer .NET 10 when an SDK is available; fall back to the installed SDK target if the local machine lacks .NET 10.
- Persist data in `basketball.db` with a `schema_info` version table.
- Store imported photos under `photos/`; store only relative photo paths in SQLite.

## Trade-offs

- SQLite is now the primary store. The app still keeps a simple in-memory `AppData` object and saves it transactionally, which is enough for the single-operator desktop scope.
- Direct incremental repository methods can be added when data size or concurrency requires them.
