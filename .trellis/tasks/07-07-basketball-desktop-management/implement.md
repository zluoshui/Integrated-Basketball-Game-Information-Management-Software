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
- Run available build/validation commands and report any SDK blockers.

## Validation

- `dotnet --info`
- `dotnet build` if a compatible SDK is available.
- Manual smoke path: create player, add custom field, create match, add roster player, record events, undo, restart.

## Rollback Points

- The task is a new project in an otherwise empty workspace, so rollback is deleting the new app files and this task directory.
