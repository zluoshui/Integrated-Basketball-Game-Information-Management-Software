# Implementation Plan

## Steps

- Create a single WPF project skeleton.
- Add domain models and JSON data store.
- Build `MainWindow` with four tabs: players, match setup, score table, event log.
- Implement event append, undo, and statistics recomputation.
- Implement clock start, pause, resume, and reset.
- Fix player save state so repeated adds do not overwrite the last selected player.
- Add a root-level test launch batch file.
- Initialize git and commit the current work.
- Complete phase 0 README fixes requested by review.
- Replace JSON primary persistence with SQLite and schema versioning.
- Keep legacy JSON import as a migration path.
- Add player photo preview, delete confirmation, student-number uniqueness, and manual backup.
- Preserve player/event consistency by disabling players with historical events.
- Add migration framework and schema v2 team/match fields.
- Add phase 2 team management, structured match creation, and roster validation.
- Add phase 3 entry hardening: roster presence check before clock start, team disable confirmation/usage prompt, migration transaction, match note input, and README self-check wording.
- Add phase 3 clock controls: persisted match status, next-period, finish-match, startup auto-pause recovery, status-driven button enablement, and event-recording guard.
- Add phase 4 entry safeguards: disable/guard next-period while running, confirm reset-current-period, and define no-direct-undo for finished matches.
- Add phase 4 event recording improvements: event notes, home/away player filter, event-log note display, and broader self-check coverage for player statistics.
- Add phase 5 logs/reports: audit-style event voiding, event filters, team statistics, CSV export, and self-check coverage for voided event projection.
- Change CSV export to use a preset export directory with project-level `exports` as the default; clicking export writes directly to that location.
- Run available build/validation commands and report any SDK blockers.

## Validation

- `dotnet --info`
- `dotnet build` if a compatible SDK is available.
- `dotnet list src/BasketballManager/BasketballManager.csproj package --vulnerable --include-transitive`
- Manual smoke path: create player, add custom field, create match, add roster player, record events, undo, restart.

## Rollback Points

- The task is a new project in an otherwise empty workspace, so rollback is deleting the new app files and this task directory.
