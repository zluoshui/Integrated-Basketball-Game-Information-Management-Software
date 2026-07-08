namespace BasketballManager;

public static class MatchLogService
{
    public const string FormatName = "BasketballManager.MatchLog";
    public const int FormatVersion = 1;

    public static MatchLogExport Build(AppData data, string competitionId, Match match)
    {
        var rosters = data.Rosters
            .Where(roster => roster.MatchId == match.Id)
            .OrderBy(roster => roster.Side)
            .ThenBy(roster => roster.JerseyNumber)
            .ToList();
        var rosterPlayerIds = rosters.Select(roster => roster.PlayerId).ToHashSet();
        var eventPlayerIds = data.Events
            .Where(item => item.MatchId == match.Id && item.PlayerId is not null)
            .Select(item => item.PlayerId!.Value)
            .ToHashSet();
        var relatedPlayerIds = data.Events
            .Where(item => item.MatchId == match.Id && item.RelatedPlayerId is not null)
            .Select(item => item.RelatedPlayerId!.Value)
            .ToHashSet();
        var playerIds = rosterPlayerIds.Concat(eventPlayerIds).Concat(relatedPlayerIds).ToHashSet();
        var teamIds = new[] { match.HomeTeamId, match.AwayTeamId }
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();
        foreach (var player in data.Players.Where(player => playerIds.Contains(player.Id) && player.TeamId is not null))
        {
            teamIds.Add(player.TeamId!.Value);
        }

        return new MatchLogExport
        {
            CompetitionId = competitionId,
            ExportedAt = DateTime.UtcNow,
            MatchId = match.Id,
            Match = match,
            Teams = data.Teams.Where(team => teamIds.Contains(team.Id)).OrderBy(team => team.Name).ToList(),
            Players = data.Players.Where(player => playerIds.Contains(player.Id)).OrderBy(player => player.Name).ToList(),
            Rosters = rosters,
            Events = data.Events.Where(item => item.MatchId == match.Id).OrderBy(item => item.CreatedAt).ToList()
        };
    }

    public static MatchLogImportResult Import(AppData data, MatchLogExport export, string currentCompetitionId)
    {
        if (!export.FormatName.Equals(FormatName, StringComparison.Ordinal)
            || export.FormatVersion != FormatVersion)
        {
            return MatchLogImportResult.Rejected($"不支持的比赛日志格式：{export.FormatName} v{export.FormatVersion}");
        }

        if (!export.CompetitionId.Equals(currentCompetitionId, StringComparison.Ordinal))
        {
            return MatchLogImportResult.Rejected($"日志归属赛事 ID 为“{export.CompetitionId}”，当前赛事 ID 为“{currentCompetitionId}”。");
        }

        if (export.Match is null || export.Match.Id != export.MatchId)
        {
            return MatchLogImportResult.Rejected("比赛日志中的比赛 ID 与比赛快照不一致。");
        }

        if (data.Matches.Any(match => match.Id == export.MatchId))
        {
            return MatchLogImportResult.Skipped("重复比赛已跳过。");
        }

        var availablePlayerIds = data.Players.Select(player => player.Id).Concat(export.Players.Select(player => player.Id)).ToHashSet();
        var requiredPlayerIds = export.Rosters
            .Where(roster => roster.MatchId == export.MatchId)
            .Select(roster => roster.PlayerId)
            .Concat(export.Events.Where(item => item.MatchId == export.MatchId && item.PlayerId is not null).Select(item => item.PlayerId!.Value))
            .Concat(export.Events.Where(item => item.MatchId == export.MatchId && item.RelatedPlayerId is not null).Select(item => item.RelatedPlayerId!.Value))
            .Distinct()
            .ToList();
        var missingPlayerCount = requiredPlayerIds.Count(playerId => !availablePlayerIds.Contains(playerId));
        if (missingPlayerCount > 0)
        {
            return MatchLogImportResult.Rejected($"比赛日志缺少 {missingPlayerCount} 个名单或事件引用的球员快照。");
        }

        foreach (var team in export.Teams.Where(team => data.Teams.All(existing => existing.Id != team.Id)))
        {
            data.Teams.Add(team);
        }

        foreach (var player in export.Players.Where(player => data.Players.All(existing => existing.Id != player.Id)))
        {
            data.Players.Add(player);
        }

        data.Matches.Add(export.Match);
        foreach (var roster in export.Rosters.Where(roster => roster.MatchId == export.MatchId))
        {
            if (data.Rosters.All(existing => existing.Id != roster.Id))
            {
                data.Rosters.Add(roster);
            }
        }

        foreach (var item in export.Events.Where(item => item.MatchId == export.MatchId))
        {
            if (data.Events.All(existing => existing.Id != item.Id))
            {
                data.Events.Add(item);
            }
        }

        return MatchLogImportResult.Imported();
    }
}

public sealed class MatchLogImportResult
{
    private MatchLogImportResult(MatchLogImportStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public MatchLogImportStatus Status { get; }
    public string Message { get; }

    public static MatchLogImportResult Imported() => new(MatchLogImportStatus.Imported, "导入成功。");
    public static MatchLogImportResult Skipped(string message) => new(MatchLogImportStatus.Skipped, message);
    public static MatchLogImportResult Rejected(string message) => new(MatchLogImportStatus.Rejected, message);
}

public enum MatchLogImportStatus
{
    Imported,
    Skipped,
    Rejected
}
