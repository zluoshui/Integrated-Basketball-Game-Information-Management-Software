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

        foreach (var item in data.Events.Where(e => e.MatchId == match.Id && !e.IsVoided).OrderBy(e => e.CreatedAt))
        {
            statsByPlayer.TryGetValue(item.PlayerId ?? Guid.Empty, out var playerStats);

            switch (item.Kind)
            {
                case MatchEventKind.Score:
                    if (playerStats is null)
                    {
                        continue;
                    }

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
                    if (playerStats is null)
                    {
                        continue;
                    }

                    playerStats.Fouls++;
                    var periodStats = projection.PeriodTeamStats.FirstOrDefault(stats => stats.Period == item.Period);
                    if (periodStats is null)
                    {
                        periodStats = new PeriodTeamStats { Period = item.Period };
                        projection.PeriodTeamStats.Add(periodStats);
                    }

                    if (item.Side == TeamSide.Home)
                    {
                        projection.HomeFouls++;
                        periodStats.HomeFouls++;
                    }
                    else
                    {
                        projection.AwayFouls++;
                        periodStats.AwayFouls++;
                    }
                    break;
                case MatchEventKind.Rebound:
                    if (playerStats is null)
                    {
                        continue;
                    }

                    playerStats.Rebounds++;
                    break;
                case MatchEventKind.Assist:
                    if (playerStats is null)
                    {
                        continue;
                    }

                    playerStats.Assists++;
                    break;
                case MatchEventKind.Steal:
                    if (playerStats is null)
                    {
                        continue;
                    }

                    playerStats.Steals++;
                    break;
                case MatchEventKind.Block:
                    if (playerStats is null)
                    {
                        continue;
                    }

                    playerStats.Blocks++;
                    break;
                case MatchEventKind.Turnover:
                    if (playerStats is null)
                    {
                        continue;
                    }

                    playerStats.Turnovers++;
                    break;
                case MatchEventKind.TimeoutRequest:
                    if (playerStats is not null)
                    {
                        playerStats.TimeoutRequests++;
                    }

                    if (item.Side == TeamSide.Home)
                    {
                        projection.HomeTimeouts++;
                    }
                    else
                    {
                        projection.AwayTimeouts++;
                    }
                    break;
                case MatchEventKind.ClockControl:
                    break;
                case MatchEventKind.RosterAudit:
                    break;
            }
        }

        projection.PeriodTeamStats = projection.PeriodTeamStats
            .OrderBy(item => item.Period)
            .ToList();
        projection.PlayerStats = statsByPlayer.Values
            .OrderBy(item => item.Side)
            .ThenBy(item => item.PlayerName)
            .ToList();
        return projection;
    }
}
