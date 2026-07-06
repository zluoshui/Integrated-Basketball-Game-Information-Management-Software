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
- Run available build/validation commands and report any SDK blockers.

## Validation

- `dotnet --info`
- `dotnet build` if a compatible SDK is available.
- `dotnet list src/BasketballManager/BasketballManager.csproj package --vulnerable --include-transitive`
- Manual smoke path: create player, add custom field, create match, add roster player, record events, undo, restart.

## Rollback Points

- The task is a new project in an otherwise empty workspace, so rollback is deleting the new app files and this task directory.
