# Basketball Desktop Management System

## Goal

Build a Windows 10/11 desktop MVP for offline basketball player management and score table workflows. The first version must let one operator maintain players, prepare a match roster, run a game clock, record score table events, undo the last event, and persist data locally.

## Confirmed Facts

- The repository has no existing application code to reuse.
- The user approved creating this Trellis task.
- Target product shape: Windows desktop, offline single-machine use.
- Preferred plan: C# WPF with local persistence.
- Local machine currently has .NET 7 runtime only and no .NET SDK, so compilation may require installing an SDK later.

## Requirements

- Player management must support creating, editing, deleting, and searching players.
- Player records must include name, student number, team, note, photo path, status, created time, and updated time.
- Custom player fields must be supported through user-defined field definitions and per-player values.
- Match setup must support creating a match, selecting home/away teams, and assigning roster players with jersey numbers and starter flags.
- Score table must support period number, countdown seconds, start, pause, resume, reset, score, timeout count, and team foul count.
- Match events must record player-scoped 1/2/3 point scores, fouls, rebounds, assists, steals, blocks, turnovers, and timeout requests.
- Timeout requests must belong to a player and also increment the player's team timeout count.
- All match mutations must append an event log entry.
- Undo/correction must preserve event history by marking events as voided with reason/time/operator metadata, then recompute visible score/statistics from effective events.
- Data must persist locally after application restart.
- Adding a player must leave the form ready for the next new player in the same app session.
- A local batch file must start the app for testing without packaging.
- The workspace must be initialized as a git repository for traceable rollback.
- Phase 0 must provide README build/run/publish instructions with generic paths and documented validation environment.
- Phase 1 must use SQLite as the primary structured data store with a schema version table.
- Phase 1 must keep legacy JSON import available only as migration input.
- Player deletion must require confirmation.
- Player photos must be copied into the app data directory and previewed when available.
- Missing photo files must show a plain placeholder message instead of crashing.
- Student numbers must be unique when provided.
- Users must be able to manually back up the database and photo directory.
- Players with historical match events must not be physically deleted; they must be disabled to preserve event consistency.
- Database migration code must exist even for additive schema changes.
- Corrupt player photo files must not crash the window.
- Repeated backup clicks in the same second must create distinct backup folders.
- Phase 2 must support creating, editing, and disabling teams.
- Phase 2 match creation must select structured home and away teams, set date/location, period count, and period length.
- Phase 2 roster setup must reject duplicate players, duplicate jersey numbers on one side, and players assigned to the wrong side when they have a team relationship.
- Before the match clock starts, both home and away rosters must contain at least one player.
- Disabling a team must show confirmation, and the prompt must state when the team is already used by players or matches.
- Schema migrations must run inside a transaction.
- Match creation must include a note field in the UI and persist it.
- README self-check text must describe the current SQLite/team/backup coverage.
- Phase 3 must support match clock statuses: not started, running, paused, interval, and finished.
- Phase 3 must support next-period and finish-match controls.
- Clock state must persist and reopening the app must handle previously running matches with a clear automatic pause strategy.
- Match events must be blocked unless the selected match is running.
- Running matches must not advance to the next period by accidental click; the next-period control is disabled while running and guarded in code.
- Resetting the current period clock must require confirmation.
- Finished matches must not allow direct event undo; later corrections should use a void/correction record so audit history is preserved.
- Phase 4 must support event notes and persist them in the event log.
- Phase 4 must make home/away player selection clearer than one unfiltered player list.
- Phase 5 must show match logs with filters by side, event kind, and player text.
- Phase 5 must show team statistics and player statistics derived from effective event history.
- Phase 5 must export a CSV report containing match info, team statistics, player statistics, and full event log.
- CSV export must use a preset export directory. The default directory is the project-level `exports` folder, and clicking export must write directly there without asking for a file path.
- Voided events must remain in the event log but be excluded from score and player/team statistics.
- Out of scope: accounts, networking, cloud sync, multi-device collaboration, league rule engines, and installer packaging beyond project-level publish support.

## Acceptance Criteria

- [ ] The app can create a player with a student number, team, note, and custom field value.
- [ ] The app can create a match and add players to home and away rosters.
- [ ] The app can start, pause, resume, and reset the match clock.
- [ ] Recording score, foul, rebound, assist, steal, block, turnover, and timeout events updates score table state and player statistics.
- [ ] Voiding the last effective event rolls back score table state and player statistics while preserving audit metadata.
- [ ] Closing and reopening the app keeps players, custom fields, matches, rosters, and events.
- [ ] Multiple players can be added in one app session without overwriting the previous player.
- [ ] A root-level batch file starts the local test app.
- [ ] A validation command documents whether the project builds on the current machine.
- [ ] README uses a generic dotnet path placeholder and documents .NET SDK / Windows validation environment.
- [ ] Player data, custom fields, matches, rosters, and events persist in SQLite.
- [ ] Legacy JSON data imports into SQLite on first load when the database is empty.
- [ ] Deleting a player asks for confirmation.
- [ ] Player photo preview works, and a missing file shows a non-crashing placeholder.
- [ ] Duplicate non-empty student numbers are rejected.
- [ ] Manual backup creates a timestamped copy of the database and photos.
- [ ] Deleting a player with existing events disables the player instead of deleting event history.
- [ ] `MigrateDatabase()` handles current schema upgrades.
- [ ] Corrupt image files show a readable placeholder message.
- [ ] Two quick backups do not fail due to folder name collision.
- [ ] Teams can be created, edited, and disabled.
- [ ] A match can be created by selecting two different active teams, date/location, period count, and period length.
- [ ] Roster setup rejects duplicate players, same-side jersey conflicts, and wrong-team roster assignment.
- [ ] Starting the clock is blocked when either side has an empty roster.
- [ ] Team disable asks for confirmation and warns when the team is already used.
- [ ] `MigrateDatabase()` wraps schema upgrades in a transaction.
- [ ] Match notes can be entered from the match setup UI and saved.
- [ ] README self-check coverage matches the current SQLite/team/backup behavior.
- [ ] Scoreboard shows current match status and enables controls according to that status.
- [ ] Next-period advances the period and resets the period clock.
- [ ] Finish-match stops timing and prevents further clock/event recording.
- [ ] Reopening after an active clock automatically pauses the match and shows a clear prompt.
- [ ] SQLite persists match status, period, and remaining clock seconds.
- [ ] Running matches cannot advance to the next period without first stopping the clock.
- [ ] Reset current period asks for confirmation.
- [ ] Finished matches show a clear no-direct-undo rule.
- [ ] Event records can include a note and the note survives restart.
- [ ] Score table recording can filter active players by home/away side.
- [ ] Event log can be filtered by side, event kind, and player text.
- [ ] Team statistics and player statistics ignore voided events.
- [ ] CSV export opens in common office software and includes Chinese-readable match data, statistics, and full event audit log.
- [ ] CSV export location can be preset, defaults to the project `exports` folder, and export does not reopen a save dialog.
- [ ] Voided events keep original event data plus void time, reason, and optional operator.

## Notes

- Keep the implementation small. Prefer one WPF project, simple services, and no speculative plugin/module system.
- After phase 4 behavior settles, extract clock/status transitions into `ClockService` and event/roster validation into `MatchService` so state-flow tests can run outside WPF.
