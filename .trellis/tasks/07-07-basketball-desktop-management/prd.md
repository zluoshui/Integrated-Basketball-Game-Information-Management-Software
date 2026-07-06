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
- Undo must remove the latest event and recompute visible score/statistics from remaining events.
- Data must persist locally after application restart.
- Adding a player must leave the form ready for the next new player in the same app session.
- A local batch file must start the app for testing without packaging.
- The workspace must be initialized as a git repository for traceable rollback.
- Out of scope: accounts, networking, cloud sync, multi-device collaboration, league rule engines, and installer packaging beyond project-level publish support.

## Acceptance Criteria

- [ ] The app can create a player with a student number, team, note, and custom field value.
- [ ] The app can create a match and add players to home and away rosters.
- [ ] The app can start, pause, resume, and reset the match clock.
- [ ] Recording score, foul, rebound, assist, steal, block, turnover, and timeout events updates score table state and player statistics.
- [ ] Undoing the last event rolls back score table state and player statistics.
- [ ] Closing and reopening the app keeps players, custom fields, matches, rosters, and events.
- [ ] Multiple players can be added in one app session without overwriting the previous player.
- [ ] A root-level batch file starts the local test app.
- [ ] A validation command documents whether the project builds on the current machine.

## Notes

- Keep the implementation small. Prefer one WPF project, simple services, and no speculative plugin/module system.
