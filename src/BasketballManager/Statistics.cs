namespace BasketballManager;

public static class Statistics
{
    public static ScoreProjection Compute(AppData data, Match match)
    {
        var projection = new ScoreProjection();
        var roster = data.Rosters.Where(item => item.MatchId == match.Id).ToList();
        var statsByPlayer = roster.ToDictionary(
            item => item.PlayerId,
            item =>
            {
                var player = data.Players.FirstOrDefault(p => p.Id == item.PlayerId);
                return new PlayerStats
                {
                    PlayerName = player?.DisplayName ?? "未知球员",
                    Side = item.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName
                };
            });

        foreach (var item in data.Events.Where(e => e.MatchId == match.Id).OrderBy(e => e.CreatedAt))
        {
            if (!statsByPlayer.TryGetValue(item.PlayerId, out var playerStats))
            {
                continue;
            }

            switch (item.Kind)
            {
                case MatchEventKind.Score:
                    playerStats.Points += item.Points;
                    if (item.Side == TeamSide.Home)
                    {
                        projection.HomeScore += item.Points;
                    }
                    else
                    {
                        projection.AwayScore += item.Points;
                    }
                    break;
                case MatchEventKind.Foul:
                    playerStats.Fouls++;
                    if (item.Side == TeamSide.Home)
                    {
                        projection.HomeFouls++;
                    }
                    else
                    {
                        projection.AwayFouls++;
                    }
                    break;
                case MatchEventKind.Rebound:
                    playerStats.Rebounds++;
                    break;
                case MatchEventKind.Assist:
                    playerStats.Assists++;
                    break;
                case MatchEventKind.Steal:
                    playerStats.Steals++;
                    break;
                case MatchEventKind.Block:
                    playerStats.Blocks++;
                    break;
                case MatchEventKind.Turnover:
                    playerStats.Turnovers++;
                    break;
                case MatchEventKind.TimeoutRequest:
                    playerStats.TimeoutRequests++;
                    if (item.Side == TeamSide.Home)
                    {
                        projection.HomeTimeouts++;
                    }
                    else
                    {
                        projection.AwayTimeouts++;
                    }
                    break;
            }
        }

        projection.PlayerStats = statsByPlayer.Values
            .OrderBy(item => item.Side)
            .ThenBy(item => item.PlayerName)
            .ToList();
        return projection;
    }
}
