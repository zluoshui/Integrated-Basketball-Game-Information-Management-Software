# Design

## Architecture

Create one WPF desktop project with three small layers:

- UI: `MainWindow` with tabs for players, match setup, score table, and event log.
- Domain: plain C# models for players, custom fields, teams, matches, rosters, events, clock state, and derived statistics.
- Persistence: one local store service that loads/saves all data in one file under the user's application data directory.

Use JSON persistence for this MVP if SQLite packages cannot be restored locally. Keep the storage service interface narrow so it can be replaced by SQLite later without changing the UI event flow.

## Data Flow

- UI edits mutate an in-memory `AppData` object through `DataStore`.
- Player and match lists bind to observable collections.
- Score table buttons append `MatchEvent` records.
- Derived score, team fouls, timeout counts, and player statistics are recomputed from event history after each event or undo.
- Clock state uses a dispatcher timer and elapsed wall-clock deltas instead of subtracting exactly one second per tick.

## Compatibility

- Target WPF on Windows x64.
- Prefer .NET 10 when an SDK is available; fall back to the installed SDK target if the local machine lacks .NET 10.
- Persist data in UTF-8 JSON with a `SchemaVersion` integer so future SQLite migration has one explicit source shape.

## Trade-offs

- JSON persistence is the smallest offline store that works without NuGet or native SQLite binaries. It is acceptable for the first local MVP and keeps implementation unblockable on the current machine.
- SQLite remains a follow-up once SDK/package restore is available.
