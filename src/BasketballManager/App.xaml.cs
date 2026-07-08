using System.Windows;
using System.IO;
using Microsoft.Data.Sqlite;

namespace BasketballManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--self-check", StringComparer.OrdinalIgnoreCase))
        {
            SelfCheck.Run();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }
}

internal static class SelfCheck
{
    public static void Run()
    {
        var homeTeam = new Team { Name = "A" };
        var awayTeam = new Team { Name = "B" };
        var homePlayer = new Player { Name = "Home Test", StudentNumber = "001", TeamId = homeTeam.Id, Team = homeTeam.Name };
        var awayPlayer = new Player { Name = "Away Test", StudentNumber = "002", TeamId = awayTeam.Id, Team = awayTeam.Name };
        var match = new Match
        {
            Name = "MVP",
            HomeTeamId = homeTeam.Id,
            AwayTeamId = awayTeam.Id,
            HomeTeamName = homeTeam.Name,
            AwayTeamName = awayTeam.Name,
            ScheduledAt = DateTime.UtcNow.Date,
            Location = "Gym",
            PeriodCount = 4,
            Status = MatchStatus.Paused,
            CurrentPeriod = 2,
            RemainingSeconds = 123
        };
        var data = new AppData
        {
            Players = [homePlayer, awayPlayer],
            PlayerFields = [new PlayerFieldDefinition { Name = "位置", DisplayOrder = 0 }],
            Teams = [homeTeam, awayTeam],
            Matches = [match],
            Rosters =
            [
                new MatchRoster { MatchId = match.Id, PlayerId = homePlayer.Id, Side = TeamSide.Home, IsStarter = true, IsOnCourt = true },
                new MatchRoster { MatchId = match.Id, PlayerId = awayPlayer.Id, Side = TeamSide.Away, IsStarter = true, IsOnCourt = true }
            ]
        };
        data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = homePlayer.Id, FieldId = data.PlayerFields[0].Id, Value = "后卫" });

        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = homePlayer.Id, Side = TeamSide.Home, Kind = MatchEventKind.Score, Points = 2, Note = "快攻" });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = awayPlayer.Id, Side = TeamSide.Away, Kind = MatchEventKind.Score, Points = 3 });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = homePlayer.Id, Side = TeamSide.Home, Kind = MatchEventKind.Foul, Period = 2 });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = awayPlayer.Id, Side = TeamSide.Away, Kind = MatchEventKind.TimeoutRequest });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = null, Side = TeamSide.Home, Kind = MatchEventKind.TimeoutRequest });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = null, Side = TeamSide.Home, Kind = MatchEventKind.ClockControl, Period = 2, Note = "提前进入下一节" });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = null, Side = TeamSide.Home, Kind = MatchEventKind.RosterAudit, Period = 2, Note = "名单审计修改：更新名单；操作人：自检；备注：测试" });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = homePlayer.Id, RelatedPlayerId = homePlayer.Id, Side = TeamSide.Home, Kind = MatchEventKind.Substitution, Period = 2, Note = "换人测试" });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = homePlayer.Id, Side = TeamSide.Home, Kind = MatchEventKind.Rebound });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = homePlayer.Id, Side = TeamSide.Home, Kind = MatchEventKind.Assist });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = awayPlayer.Id, Side = TeamSide.Away, Kind = MatchEventKind.Steal });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = homePlayer.Id, Side = TeamSide.Home, Kind = MatchEventKind.Block });
        data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = awayPlayer.Id,
            Side = TeamSide.Away,
            Kind = MatchEventKind.Turnover,
            IsVoided = true,
            VoidedAt = DateTime.UtcNow,
            VoidReason = "录入错误",
            VoidedBy = "自检"
        });

        var projection = Statistics.Compute(data, match);
        Assert(projection.HomeScore == 2, "home score projection failed");
        Assert(projection.AwayScore == 3, "away score projection failed");
        Assert(projection.HomeFouls == 1, "foul projection failed");
        Assert(projection.PeriodTeamStats.Any(item => item.Period == 2 && item.HomeFouls == 1), "period foul projection failed");
        Assert(projection.HomeTimeouts == 1, "team timeout projection failed");
        Assert(projection.AwayTimeouts == 1, "timeout projection failed");
        Assert(projection.PlayerStats.Any(item => item.PlayerName == homePlayer.DisplayName && item.Rebounds == 1 && item.Assists == 1 && item.Blocks == 1), "home player stats projection failed");
        Assert(projection.PlayerStats.Any(item => item.PlayerName == awayPlayer.DisplayName && item.Steals == 1 && item.Turnovers == 0), "away player stats projection failed");

        var matchLog = MatchLogService.Build(data, "SchoolCup2026", match);
        Assert(matchLog.CompetitionId == "SchoolCup2026", "match log competition id failed");
        Assert(matchLog.MatchId == match.Id, "match log match id failed");
        Assert(matchLog.Events.Any(item => item.Id == data.Events[0].Id), "match log event id failed");
        Assert(matchLog.Events.Any(item => item.Kind == MatchEventKind.Substitution && item.RelatedPlayerId == homePlayer.Id), "match log substitution failed");
        var importedData = new AppData();
        var importResult = MatchLogService.Import(importedData, matchLog, "SchoolCup2026");
        Assert(importResult.Status == MatchLogImportStatus.Imported, "match log import failed");
        Assert(importedData.Matches.Count == 1 && importedData.Events.Count == data.Events.Count, "match log import data failed");
        Assert(MatchLogService.Import(importedData, matchLog, "SchoolCup2026").Status == MatchLogImportStatus.Skipped, "duplicate match log skip failed");
        Assert(MatchLogService.Import(new AppData(), matchLog, "OtherCup2026").Status == MatchLogImportStatus.Rejected, "foreign competition log rejection failed");

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"BasketballManagerSelfCheck-{Guid.NewGuid():N}");
        var store = new DataStore(tempDirectory);
        try
        {
            var workspaceManager = new CompetitionWorkspaceManager(Path.Combine(tempDirectory, "app"));
            var workspace = workspaceManager.CreateCompetition("校杯", "SchoolCup2026");
            Assert(File.Exists(workspace.ManifestPath), "competition manifest missing");
            Assert(File.Exists(workspace.DatabasePath), "competition database missing");
            Assert(Directory.Exists(workspace.PhotoDirectory), "competition photo directory missing");
            AssertThrows(() => CompetitionWorkspaceManager.ValidateCompetitionId("校杯 2026"), "invalid competition id failed");
            var updatedWorkspace = workspaceManager.UpdateManifest(workspace, "校杯更新", "SchoolCup2026A");
            Assert(updatedWorkspace.Manifest.CompetitionId == "SchoolCup2026A", "competition id update failed");
            var exportedWorkspacePath = workspaceManager.ExportWorkspace(updatedWorkspace, Path.Combine(tempDirectory, "workspace-exports"));
            Assert(File.Exists(Path.Combine(exportedWorkspacePath, "competition.json")), "competition export manifest missing");
            Assert(File.Exists(Path.Combine(exportedWorkspacePath, "basketball.db")), "competition export database missing");
            AssertThrows(() => workspaceManager.OpenWorkspace(exportedWorkspacePath), "external competition open should be rejected");
            AssertThrows(() => workspaceManager.ImportWorkspace(exportedWorkspacePath), "duplicate competition import should be rejected");
            var otherWorkspaceManager = new CompetitionWorkspaceManager(Path.Combine(tempDirectory, "other-app"));
            Assert(otherWorkspaceManager.ImportWorkspace(exportedWorkspacePath).Manifest.CompetitionId == "SchoolCup2026A", "competition import failed");
            Assert(otherWorkspaceManager.ListImportedWorkspaces().Count == 1, "imported competition list failed");

            store.Save(data);
            var loaded = store.Load();
            Assert(loaded.Players.Count == 2, "data store player round-trip failed");
            Assert(loaded.Teams.Count == 2, "team round-trip failed");
            Assert(loaded.Matches[0].HomeTeamId == homeTeam.Id, "match team round-trip failed");
            Assert(loaded.Matches[0].Status == MatchStatus.Paused, "match status round-trip failed");
            Assert(loaded.Matches[0].CurrentPeriod == 2, "match period round-trip failed");
            Assert(loaded.Matches[0].RemainingSeconds == 123, "match clock round-trip failed");
            Assert(loaded.PlayerFields.Count == 1, "custom field definition round-trip failed");
            Assert(loaded.PlayerFieldValues.Count == 1, "custom field value round-trip failed");
            Assert(loaded.Rosters.Any(item => item.IsStarter && item.IsOnCourt), "roster on-court round-trip failed");
            Assert(loaded.Events.Count == 13, "data store event round-trip failed");
            Assert(loaded.Events.Any(item => item.Note == "快攻"), "event note round-trip failed");
            Assert(loaded.Events.Any(item => item.Kind == MatchEventKind.Substitution && item.RelatedPlayerId == homePlayer.Id), "substitution event round-trip failed");
            Assert(loaded.Events.Any(item => item.Kind == MatchEventKind.RosterAudit && item.Note.Contains("名单审计修改", StringComparison.OrdinalIgnoreCase)), "roster audit event round-trip failed");
            Assert(loaded.Events.Any(item => item.IsVoided && item.VoidReason == "录入错误" && item.VoidedBy == "自检"), "event void audit round-trip failed");
            Assert(Statistics.Compute(loaded, loaded.Matches[0]).HomeScore == 2, "loaded home projection failed");
            Assert(Statistics.Compute(loaded, loaded.Matches[0]).AwayScore == 3, "loaded away projection failed");
            Assert(Statistics.Compute(loaded, loaded.Matches[0]).PlayerStats.Any(item => item.PlayerName == awayPlayer.DisplayName && item.Turnovers == 0), "voided event projection failed");
            var exportDirectory = Path.Combine(tempDirectory, "exports");
            store.SaveExportDirectory(exportDirectory);
            Assert(store.LoadExportDirectory() == exportDirectory, "export directory setting round-trip failed");
            Assert(File.Exists(store.DataPath), "sqlite database file missing");
            Assert(Directory.Exists(store.Backup()), "backup directory missing");
            Assert(Directory.Exists(store.Backup()), "second backup directory missing");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
