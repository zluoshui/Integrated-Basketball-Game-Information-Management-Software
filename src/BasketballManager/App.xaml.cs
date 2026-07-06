using System.Windows;

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
            Matches = [match],
            Rosters = [new MatchRoster { MatchId = match.Id, PlayerId = player.Id, Side = TeamSide.Home }]
        };

        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = player.Id, Side = TeamSide.Home, Kind = MatchEventKind.Score, Points = 3 });
        data.Events.Add(new MatchEvent { MatchId = match.Id, PlayerId = player.Id, Side = TeamSide.Home, Kind = MatchEventKind.Foul });

        var projection = Statistics.Compute(data, match);
        Assert(projection.HomeScore == 3, "score projection failed");
        Assert(projection.HomeFouls == 1, "foul projection failed");

        data.Events.RemoveAt(data.Events.Count - 1);
        projection = Statistics.Compute(data, match);
        Assert(projection.HomeFouls == 0, "undo projection failed");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
