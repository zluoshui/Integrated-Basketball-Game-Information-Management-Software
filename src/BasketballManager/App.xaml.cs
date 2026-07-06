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
        var player = new Player { Name = "Test", StudentNumber = "001" };
        var match = new Match { Name = "MVP", HomeTeamName = "A", AwayTeamName = "B" };
        var data = new AppData
        {
            Players = [player],
            PlayerFields = [new PlayerFieldDefinition { Name = "位置", DisplayOrder = 0 }],
            Matches = [match],
            Rosters = [new MatchRoster { MatchId = match.Id, PlayerId = player.Id, Side = TeamSide.Home }]
        };
        data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = data.PlayerFields[0].Id, Value = "后卫" });

        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = player.Id, Side = TeamSide.Home, Kind = MatchEventKind.Score, Points = 3 });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = player.Id, Side = TeamSide.Home, Kind = MatchEventKind.Foul });

        var projection = Statistics.Compute(data, match);
        Assert(projection.HomeScore == 3, "score projection failed");
        Assert(projection.HomeFouls == 1, "foul projection failed");

        data.Events.RemoveAt(data.Events.Count - 1);
        projection = Statistics.Compute(data, match);
        Assert(projection.HomeFouls == 0, "undo projection failed");

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"BasketballManagerSelfCheck-{Guid.NewGuid():N}");
        var store = new DataStore(tempDirectory);
        try
        {
            store.Save(data);
            var loaded = store.Load();
            Assert(loaded.Players.Count == 1, "data store player round-trip failed");
            Assert(loaded.PlayerFields.Count == 1, "custom field definition round-trip failed");
            Assert(loaded.PlayerFieldValues.Count == 1, "custom field value round-trip failed");
            Assert(loaded.Events.Count == 1, "data store event round-trip failed");
            Assert(Statistics.Compute(loaded, loaded.Matches[0]).HomeScore == 3, "loaded projection failed");
            Assert(File.Exists(store.DataPath), "sqlite database file missing");
            Assert(Directory.Exists(store.Backup()), "backup directory missing");
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
}
