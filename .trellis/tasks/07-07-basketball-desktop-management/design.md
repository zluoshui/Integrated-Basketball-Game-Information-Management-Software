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
- Derived score, team fouls, timeout counts, and player statistics are recomputed from effective event history; voided events stay in the log but are excluded from projections.
- Clock state uses a dispatcher timer and elapsed wall-clock deltas instead of subtracting exactly one second per tick.
- Match status is explicit on the `Match` model and persisted in SQLite.
- On startup, any match that was still marked as running is automatically paused and the operator is told what happened.

## Compatibility

- Target WPF on Windows x64.
- Prefer .NET 10 when an SDK is available; fall back to the installed SDK target if the local machine lacks .NET 10.
- Persist data in `basketball.db` with a `schema_info` version table.
- Store imported photos under `photos/`; store only relative photo paths in SQLite.
- Version 2 adds structured team references and match scheduling fields through `MigrateDatabase()`.
- Version 3 adds persisted match status for clock control.
- Migrations run in one SQLite transaction and update `schema_info` only after the migration steps succeed.

## Trade-offs

- SQLite is now the primary store. The app still keeps a simple in-memory `AppData` object and saves it transactionally, which is enough for the single-operator desktop scope.
- Direct incremental repository methods can be added when data size or concurrency requires them.
- Players with historical events are disabled instead of physically deleted, preserving score/event history.
- Team disabling preserves existing player and match references; the UI confirms the action and warns about existing usage.
- Clock start validation lives at the score table entry point so incomplete rosters cannot start timing.
- Event recording is allowed only while the selected match is running; this keeps status control understandable for the operator during phase 3.
- Next-period is disabled while the clock is running; period transitions should happen after pause or interval state.
- Finished matches and active matches do not directly remove historical events. Corrections mark events as voided with reason/time/operator metadata.
- CSV export is generated locally from the same event log and statistics projection used by the UI.
- CSV export writes directly to the configured export directory. The default is the project-level `exports` folder; user changes are stored under app data as `export-directory.txt`.
- Phase 4 still uses code-behind to stay small, but `ClockService` and `MatchService` are the next extraction points for testable state transitions.
