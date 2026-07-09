namespace BasketballManager.Api;

public static class ApiMapping
{
    public static string StatusText(MatchStatus status) => status switch
    {
        MatchStatus.NotStarted => "未开始",
        MatchStatus.Running => "进行中",
        MatchStatus.Paused => "暂停",
        MatchStatus.Interval => "节间",
        MatchStatus.Finished => "已结束",
        _ => status.ToString()
    };

    public static string SideText(TeamSide side) => side == TeamSide.Home ? "主队" : "客队";

    public static TeamSide ParseSide(string side) =>
        side is "客队" or "Away" or "away" ? TeamSide.Away : TeamSide.Home;

    public static MatchEventKind ParseEventKind(string kind) => kind.Trim() switch
    {
        "Score" or "得分" or "+1" or "+2" or "+3" => MatchEventKind.Score,
        "Foul" or "犯规" => MatchEventKind.Foul,
        "Rebound" or "篮板" => MatchEventKind.Rebound,
        "Assist" or "助攻" => MatchEventKind.Assist,
        "Steal" or "抢断" => MatchEventKind.Steal,
        "Block" or "盖帽" => MatchEventKind.Block,
        "Turnover" or "失误" => MatchEventKind.Turnover,
        "TimeoutRequest" or "申请暂停" or "暂停" => MatchEventKind.TimeoutRequest,
        "Substitution" or "换人" => MatchEventKind.Substitution,
        _ => throw new InvalidOperationException($"不支持的事件类型：{kind}")
    };

    public static string KindText(MatchEventKind kind) => kind switch
    {
        MatchEventKind.Score => "得分",
        MatchEventKind.Foul => "犯规",
        MatchEventKind.Rebound => "篮板",
        MatchEventKind.Assist => "助攻",
        MatchEventKind.Steal => "抢断",
        MatchEventKind.Block => "盖帽",
        MatchEventKind.Turnover => "失误",
        MatchEventKind.TimeoutRequest => "申请暂停",
        MatchEventKind.ClockControl => "计时控制",
        MatchEventKind.RosterAudit => "名单审计",
        MatchEventKind.Substitution => "换人",
        _ => kind.ToString()
    };

    public static CompetitionDto ToDto(CompetitionWorkspace workspace, bool isCurrent) =>
        new(workspace.Manifest.CompetitionId, workspace.Manifest.Name, workspace.DirectoryPath, isCurrent);

    public static TeamDto ToDto(Team team, AppData data) =>
        new(team.Id, team.Name, team.Note, team.Status, data.Players.Count(player => player.TeamId == team.Id));

    public static PlayerFieldDto ToDto(PlayerFieldDefinition field) =>
        new(field.Id, field.Name, field.FieldType, field.IsRequired, field.DisplayOrder);

    public static PlayerDto ToDto(Player player, AppData data)
    {
        var fields = data.PlayerFields.OrderBy(field => field.DisplayOrder).ToDictionary(
            field => field.Name,
            field => data.PlayerFieldValues.FirstOrDefault(value => value.PlayerId == player.Id && value.FieldId == field.Id)?.Value ?? "");
        // Keep path separators unescaped so catch-all routing and file lookup remain stable.
        var photoUrl = string.IsNullOrWhiteSpace(player.PhotoPath)
            ? ""
            : $"/api/photos/{string.Join('/', player.PhotoPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString))}?v={player.UpdatedAt.Ticks}";
        return new PlayerDto(
            player.Id,
            player.Name,
            player.StudentNumber,
            player.TeamId,
            player.Team,
            player.Note,
            player.PhotoPath,
            photoUrl,
            player.Status,
            fields);
    }

    public static MatchSummaryDto ToSummary(Match match, AppData data)
    {
        var projection = Statistics.Compute(data, match);
        return new MatchSummaryDto(
            match.Id,
            match.Name,
            match.HomeTeamName,
            match.AwayTeamName,
            match.HomeTeamId,
            match.AwayTeamId,
            StatusText(match.Status),
            projection.HomeScore,
            projection.AwayScore,
            data.Events.Count(item => item.MatchId == match.Id),
            data.Rosters.Count(item => item.MatchId == match.Id),
            match.CurrentPeriod,
            match.PeriodCount,
            match.RemainingSeconds,
            match.ScheduledAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            match.Location,
            match.Note);
    }

    public static RosterDto ToDto(MatchRoster roster, AppData data, Match match)
    {
        var player = data.Players.FirstOrDefault(item => item.Id == roster.PlayerId);
        return new RosterDto(
            roster.Id,
            roster.MatchId,
            roster.PlayerId,
            player?.DisplayName ?? "未知球员",
            SideText(roster.Side),
            roster.JerseyNumber,
            roster.IsStarter,
            roster.IsOnCourt);
    }

    public static ScoreboardDto ToScoreboard(Match match, AppData data)
    {
        var projection = Statistics.Compute(data, match);
        var period = projection.PeriodTeamStats.FirstOrDefault(item => item.Period == match.CurrentPeriod);
        var homeOnCourt = BuildOnCourt(match, data, projection, TeamSide.Home);
        var awayOnCourt = BuildOnCourt(match, data, projection, TeamSide.Away);
        return new ScoreboardDto(
            match.Id,
            match.Name,
            StatusText(match.Status),
            match.CurrentPeriod,
            match.PeriodCount,
            match.RemainingSeconds,
            match.IsClockRunning,
            projection.HomeScore,
            projection.AwayScore,
            projection.HomeFouls,
            projection.AwayFouls,
            projection.HomeTimeouts,
            projection.AwayTimeouts,
            period?.HomeFouls ?? 0,
            period?.AwayFouls ?? 0,
            match.HomeTeamName,
            match.AwayTeamName,
            homeOnCourt,
            awayOnCourt);
    }

    public static EventDto ToDto(MatchEvent item, AppData data)
    {
        var player = item.PlayerId is null ? null : data.Players.FirstOrDefault(p => p.Id == item.PlayerId);
        return new EventDto(
            item.Id,
            item.MatchId,
            item.PlayerId,
            item.RelatedPlayerId,
            player?.DisplayName,
            SideText(item.Side),
            KindText(item.Kind),
            item.Points,
            item.Period,
            item.ClockSecondsRemaining,
            item.Note,
            item.IsVoided,
            item.VoidReason,
            item.VoidedBy,
            item.CreatedAt);
    }

    private static List<OnCourtPlayerDto> BuildOnCourt(Match match, AppData data, ScoreProjection projection, TeamSide side)
    {
        return data.Rosters
            .Where(roster => roster.MatchId == match.Id && roster.Side == side && roster.IsOnCourt)
            .Select(roster =>
            {
                var player = data.Players.FirstOrDefault(item => item.Id == roster.PlayerId);
                var stats = projection.PlayerStats.FirstOrDefault(item => item.PlayerName == (player?.DisplayName ?? ""));
                return new OnCourtPlayerDto(
                    roster.PlayerId,
                    player?.Name ?? "未知球员",
                    roster.JerseyNumber,
                    SideText(side),
                    stats?.Points ?? 0,
                    stats?.Fouls ?? 0,
                    stats?.Rebounds ?? 0,
                    stats?.Assists ?? 0,
                    stats?.Steals ?? 0,
                    stats?.Blocks ?? 0,
                    stats?.Turnovers ?? 0,
                    stats?.TimeoutRequests ?? 0);
            })
            .OrderBy(item => item.JerseyNumber)
            .ThenBy(item => item.Name)
            .ToList();
    }
}
