using System.Text.Json.Serialization;
using BasketballManager;
using BasketballManager.Api;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddSingleton<AppSession>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(urls))
{
    var port = Environment.GetEnvironmentVariable("BASKETBALL_MANAGER_API_PORT");
    if (int.TryParse(port, out var p) && p > 0)
    {
        builder.WebHost.UseUrls($"http://127.0.0.1:{p}");
    }
}

var webRoot = ResolveWebRoot(builder.Environment.ContentRootPath, AppContext.BaseDirectory);
if (webRoot is not null)
{
    builder.WebHost.UseWebRoot(webRoot);
}

var app = builder.Build();
app.UseCors();

if (webRoot is not null && Directory.Exists(webRoot))
{
    var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
    Console.WriteLine($"[BasketballManager.Api] Serving SPA from: {webRoot}");
}
else
{
    Console.WriteLine("[BasketballManager.Api] WARNING: wwwroot not found. API only mode.");
}

app.MapGet("/api/health", (AppSession session) => session.Read(s => new
{
    ok = true,
    competitionId = s.Workspace.Manifest.CompetitionId,
    competitionName = s.Workspace.Manifest.Name,
    version = AppInfo.Version,
    appName = AppInfo.AppName
}));

app.MapGet("/api/app/info", () => Results.Ok(new
{
    appName = AppInfo.AppName,
    appNameEn = AppInfo.AppNameEn,
    version = AppInfo.Version,
    primaryAuthor = AppInfo.PrimaryAuthor,
    secondaryAuthors = AppInfo.SecondaryAuthors,
    githubUrl = AppInfo.GitHubRepositoryUrl,
    updateManifestUrl = AppInfo.UpdateManifestUrl
}));

app.MapGet("/api/app/update-check", async (CancellationToken cancellationToken) =>
{
    var result = await UpdateChecker.CheckAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/app/reset-demo", (AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var workspace = s.WorkspaceManager.ResetToDefaultDemoWorkspace();
            s.SwitchWorkspace(workspace, saveCurrent: false);
            return Results.Ok(ApiMapping.ToDto(s.Workspace, true));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/competitions", (AppSession session) => session.Read(s =>
{
    var current = s.Workspace.DirectoryPath;
    return s.WorkspaceManager.ListImportedWorkspaces()
        .Select(item => ApiMapping.ToDto(item, string.Equals(item.DirectoryPath, current, StringComparison.OrdinalIgnoreCase)))
        .ToList();
}));

app.MapGet("/api/competitions/current", (AppSession session) => session.Read(s => ApiMapping.ToDto(s.Workspace, true)));

app.MapPost("/api/competitions", (CreateCompetitionRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var workspace = s.WorkspaceManager.CreateCompetition(request.Name.Trim(), request.CompetitionId.Trim());
            s.SwitchWorkspace(workspace);
            return Results.Ok(ApiMapping.ToDto(workspace, true));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/competitions/import", (AppSession session, [FromQuery] string path) =>
{
    try
    {
        return session.Write(s =>
        {
            var workspace = s.WorkspaceManager.ImportWorkspace(path);
            s.SwitchWorkspace(workspace);
            return Results.Ok(ApiMapping.ToDto(workspace, true));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/competitions/{competitionId}/switch", (string competitionId, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var workspace = s.WorkspaceManager.ListImportedWorkspaces()
                .FirstOrDefault(item => item.Manifest.CompetitionId.Equals(competitionId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("未找到该赛事。");
            s.SwitchWorkspace(workspace);
            return Results.Ok(ApiMapping.ToDto(workspace, true));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapDelete("/api/competitions/{competitionId}", (string competitionId, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var currentId = s.Workspace.Manifest.CompetitionId;
            s.WorkspaceManager.DeleteCompetition(competitionId);
            if (currentId.Equals(competitionId, StringComparison.OrdinalIgnoreCase))
            {
                var next = s.WorkspaceManager.ListImportedWorkspaces().FirstOrDefault()
                    ?? s.WorkspaceManager.CreateDefaultDemoWorkspace();
                s.SwitchWorkspace(next, saveCurrent: false);
                return Results.Ok(new { deleted = competitionId, current = ApiMapping.ToDto(s.Workspace, true) });
            }

            return Results.Ok(new { deleted = competitionId, current = ApiMapping.ToDto(s.Workspace, true) });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPut("/api/competitions/current", (UpdateCompetitionRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            s.UpdateAllRunningClocks();
            s.Save();
            var workspace = s.WorkspaceManager.UpdateManifest(s.Workspace, request.Name.Trim(), request.CompetitionId.Trim());
            s.SwitchWorkspace(workspace, saveCurrent: false);
            return Results.Ok(ApiMapping.ToDto(workspace, true));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/competitions/current/export", (AppSession session, [FromQuery] string targetRoot) =>
{
    try
    {
        return session.Write(s =>
        {
            s.UpdateAllRunningClocks();
            s.Save();
            var path = s.WorkspaceManager.ExportWorkspace(s.Workspace, targetRoot);
            return Results.Ok(new { path });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/teams", (AppSession session) => session.Read(s =>
    s.Data.Teams.OrderBy(team => team.Status).ThenBy(team => team.Name)
        .Select(team => ApiMapping.ToDto(team, s.Data)).ToList()));

app.MapPost("/api/teams", (CreateTeamRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("队伍名称不能为空。");
            if (s.Data.Teams.Any(team => team.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("已存在同名队伍。");
            var team = new Team { Name = name, Note = request.Note?.Trim() ?? "", Status = "启用" };
            s.Data.Teams.Add(team);
            s.Save();
            return Results.Ok(ApiMapping.ToDto(team, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPut("/api/teams/{id:guid}", (Guid id, UpdateTeamRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var team = s.Data.Teams.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("队伍不存在。");
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("队伍名称不能为空。");
            if (s.Data.Teams.Any(item => item.Id != id && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("已存在同名队伍。");
            team.Name = name;
            team.Note = request.Note?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(request.Status)) team.Status = request.Status.Trim();
            foreach (var player in s.Data.Players.Where(player => player.TeamId == team.Id)) player.Team = team.Name;
            s.Save();
            return Results.Ok(ApiMapping.ToDto(team, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/teams/{id:guid}/disable", (Guid id, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var team = s.Data.Teams.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("队伍不存在。");
            team.Status = "停用";
            s.Save();
            return Results.Ok(ApiMapping.ToDto(team, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/players", (AppSession session, [FromQuery] Guid? teamId) => session.Read(s =>
{
    var query = s.Data.Players.AsEnumerable();
    if (teamId is not null) query = query.Where(player => player.TeamId == teamId);
    return query.OrderBy(player => player.Team).ThenBy(player => player.Name)
        .Select(player => ApiMapping.ToDto(player, s.Data)).ToList();
}));

app.MapPost("/api/players", (CreatePlayerRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var player = PlayerOps.Upsert(s, null, request.Name, request.StudentNumber, request.TeamId, request.Note, request.Status, request.CustomFields);
            s.Save();
            return Results.Ok(ApiMapping.ToDto(player, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPut("/api/players/{id:guid}", (Guid id, UpdatePlayerRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var player = PlayerOps.Upsert(s, id, request.Name, request.StudentNumber, request.TeamId, request.Note, request.Status, request.CustomFields);
            s.Save();
            return Results.Ok(ApiMapping.ToDto(player, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/players/{id:guid}/photo", async (Guid id, HttpRequest request, AppSession session) =>
{
    try
    {
        if (!request.HasFormContentType) return Results.BadRequest(new ApiError("请上传图片文件。"));
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0) return Results.BadRequest(new ApiError("未选择照片文件。"));
        var tempPath = Path.Combine(Path.GetTempPath(), $"bm-photo-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
        await using (var stream = File.Create(tempPath)) await file.CopyToAsync(stream);
        return session.Write(s =>
        {
            var player = s.Data.Players.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("球员不存在。");
            player.PhotoPath = s.Store.ImportPhoto(tempPath);
            player.UpdatedAt = DateTime.UtcNow;
            s.Save();
            try { File.Delete(tempPath); } catch { }
            return Results.Ok(ApiMapping.ToDto(player, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/player-fields", (AppSession session) => session.Read(s =>
    s.Data.PlayerFields.OrderBy(field => field.DisplayOrder).Select(ApiMapping.ToDto).ToList()));

app.MapPost("/api/player-fields", (CreatePlayerFieldRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("字段名称不能为空。");
            if (s.Data.PlayerFields.Any(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("字段名称已存在。");
            var field = new PlayerFieldDefinition
            {
                Name = name,
                FieldType = string.IsNullOrWhiteSpace(request.FieldType) ? "Text" : request.FieldType.Trim(),
                IsRequired = request.IsRequired,
                DisplayOrder = s.Data.PlayerFields.Count == 0 ? 0 : s.Data.PlayerFields.Max(item => item.DisplayOrder) + 1
            };
            s.Data.PlayerFields.Add(field);
            s.Save();
            return Results.Ok(ApiMapping.ToDto(field));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapDelete("/api/player-fields/{id:guid}", (Guid id, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var field = s.Data.PlayerFields.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("字段不存在。");
            s.Data.PlayerFields.Remove(field);
            s.Data.PlayerFieldValues.RemoveAll(value => value.FieldId == id);
            s.Save();
            return Results.Ok(new { ok = true });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/matches", (AppSession session) => session.Read(s =>
{
    s.UpdateAllRunningClocks();
    return s.Data.Matches
        .OrderByDescending(match => match.ScheduledAt ?? match.CreatedAt)
        .ThenByDescending(match => match.CreatedAt)
        .Select(match => ApiMapping.ToSummary(match, s.Data)).ToList();
}));

app.MapPost("/api/matches", (CreateMatchRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var home = s.Data.Teams.FirstOrDefault(team => team.Id == request.HomeTeamId && team.Status == "启用")
                ?? throw new InvalidOperationException("请选择有效主队。");
            var away = s.Data.Teams.FirstOrDefault(team => team.Id == request.AwayTeamId && team.Status == "启用")
                ?? throw new InvalidOperationException("请选择有效客队。");
            if (home.Id == away.Id) throw new InvalidOperationException("主队和客队不能相同。");
            if (string.IsNullOrWhiteSpace(request.ScheduledAt) || !DateTime.TryParse(request.ScheduledAt, out var parsed))
                throw new InvalidOperationException("请选择比赛日期。");
            var periodCount = request.PeriodCount <= 0 ? 4 : request.PeriodCount;
            var periodLength = Math.Max(1, request.PeriodLengthMinutes) * 60;
            var scheduledAt = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Local).ToUniversalTime();
            var match = new Match
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"{home.Name} vs {away.Name}" : request.Name.Trim(),
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                HomeTeamName = home.Name,
                AwayTeamName = away.Name,
                ScheduledAt = scheduledAt,
                Location = request.Location?.Trim() ?? "",
                Note = request.Note?.Trim() ?? "",
                PeriodCount = periodCount,
                PeriodLengthSeconds = periodLength,
                RemainingSeconds = periodLength,
                Status = MatchStatus.NotStarted
            };
            s.Data.Matches.Add(match);
            s.Save();
            return Results.Ok(ApiMapping.ToSummary(match, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapDelete("/api/matches/{id:guid}", (Guid id, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(id) ?? throw new InvalidOperationException("比赛不存在。");
            if (match.Status != MatchStatus.NotStarted)
                throw new InvalidOperationException("只能删除未开始的比赛。");
            if (s.Data.Events.Any(item => item.MatchId == id && !item.IsVoided))
                throw new InvalidOperationException("该比赛已有事件，不能删除。");
            s.Data.Rosters.RemoveAll(item => item.MatchId == id);
            s.Data.Events.RemoveAll(item => item.MatchId == id);
            s.Data.Matches.RemoveAll(item => item.Id == id);
            s.Save();
            return Results.Ok(new { deleted = id });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/matches/{id:guid}", (Guid id, AppSession session) => session.Read(s =>
{
    var match = s.FindMatch(id);
    return match is null ? Results.NotFound(new ApiError("比赛不存在。")) : Results.Ok(ApiMapping.ToSummary(match, s.Data));
}));

app.MapGet("/api/matches/{id:guid}/roster", (Guid id, AppSession session) => session.Read(s =>
{
    var match = s.FindMatch(id);
    if (match is null) return Results.NotFound(new ApiError("比赛不存在。"));
    var rows = s.Data.Rosters.Where(item => item.MatchId == id)
        .OrderBy(item => item.Side).ThenBy(item => item.JerseyNumber)
        .Select(item => ApiMapping.ToDto(item, s.Data, match)).ToList();
    return Results.Ok(rows);
}));

app.MapPost("/api/matches/{id:guid}/roster", (Guid id, UpsertRosterRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(id) ?? throw new InvalidOperationException("比赛不存在。");
            var player = s.Data.Players.FirstOrDefault(item => item.Id == request.PlayerId) ?? throw new InvalidOperationException("球员不存在。");
            var side = ApiMapping.ParseSide(request.Side);
            var sideTeamId = side == TeamSide.Home ? match.HomeTeamId : match.AwayTeamId;
            if (player.TeamId is null || sideTeamId is null || player.TeamId != sideTeamId)
                throw new InvalidOperationException("正式比赛名单只能选择所选阵营队伍下的球员。");
            if (s.Data.Rosters.Any(item => item.MatchId == id && item.PlayerId == player.Id))
                throw new InvalidOperationException("该球员已在本场比赛名单中。");
            var jersey = request.JerseyNumber.Trim();
            if (!string.IsNullOrWhiteSpace(jersey) && s.Data.Rosters.Any(item => item.MatchId == id && item.Side == side && item.JerseyNumber.Equals(jersey, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"该阵营已存在 {jersey} 号球员。");
            if (match.Status == MatchStatus.NotStarted && request.IsStarter && s.Data.Rosters.Count(item => item.MatchId == id && item.Side == side && item.IsStarter) >= 5)
                throw new InvalidOperationException("同一阵营首发不能超过 5 人。");
            if (match.Status != MatchStatus.NotStarted) PlayerOps.EnsureAudit(request.AuditOperator, request.AuditNote, match);
            var roster = new MatchRoster
            {
                MatchId = id,
                PlayerId = player.Id,
                Side = side,
                JerseyNumber = jersey,
                IsStarter = request.IsStarter,
                IsOnCourt = match.Status == MatchStatus.NotStarted && request.IsStarter
            };
            s.Data.Rosters.Add(roster);
            if (match.Status != MatchStatus.NotStarted)
                PlayerOps.AddRosterAudit(s, match, side, request.AuditOperator!, request.AuditNote!, $"加入名单：{player.DisplayName}");
            s.Save();
            return Results.Ok(ApiMapping.ToDto(roster, s.Data, match));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapDelete("/api/matches/{matchId:guid}/roster/{rosterId:guid}", (Guid matchId, Guid rosterId, AppSession session, [FromQuery] string? auditOperator, [FromQuery] string? auditNote) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(matchId) ?? throw new InvalidOperationException("比赛不存在。");
            var roster = s.Data.Rosters.FirstOrDefault(item => item.Id == rosterId && item.MatchId == matchId) ?? throw new InvalidOperationException("名单记录不存在。");
            if (roster.IsOnCourt && match.Status != MatchStatus.NotStarted)
                throw new InvalidOperationException("当前场上球员不能直接移出名单，请先完成换人。");
            if (match.Status != MatchStatus.NotStarted)
            {
                PlayerOps.EnsureAudit(auditOperator, auditNote, match);
                var player = s.Data.Players.FirstOrDefault(item => item.Id == roster.PlayerId);
                PlayerOps.AddRosterAudit(s, match, roster.Side, auditOperator!, auditNote!, $"移出名单：{player?.DisplayName ?? "未知球员"}");
            }
            s.Data.Rosters.Remove(roster);
            s.Save();
            return Results.Ok(new { ok = true });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/scoreboard/{matchId:guid}", (Guid matchId, AppSession session) => session.Read(s =>
{
    var match = s.FindMatch(matchId);
    if (match is null) return Results.NotFound(new ApiError("比赛不存在。"));
    s.UpdateClockFromElapsed(match);
    return Results.Ok(ApiMapping.ToScoreboard(match, s.Data));
}));

app.MapPost("/api/scoreboard/{matchId:guid}/clock/{action}", (Guid matchId, string action, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(matchId) ?? throw new InvalidOperationException("比赛不存在。");
            s.UpdateClockFromElapsed(match);
            switch (action.ToLowerInvariant())
            {
                case "start":
                case "resume":
                    PlayerOps.EnsureCanRunClock(match, s);
                    match.Status = MatchStatus.Running;
                    match.IsClockRunning = true;
                    match.LastClockUpdateUtc = DateTime.UtcNow;
                    break;
                case "pause":
                    if (match.Status != MatchStatus.Running) throw new InvalidOperationException("只有进行中的比赛可以暂停。");
                    match.IsClockRunning = false;
                    match.LastClockUpdateUtc = null;
                    match.Status = MatchStatus.Paused;
                    break;
                case "reset":
                    if (match.Status == MatchStatus.Finished) throw new InvalidOperationException("比赛已结束，不能重置计时。");
                    match.IsClockRunning = false;
                    match.LastClockUpdateUtc = null;
                    match.RemainingSeconds = match.PeriodLengthSeconds;
                    match.Status = match.CurrentPeriod == 1 && !s.Data.Events.Any(item => item.MatchId == match.Id)
                        ? MatchStatus.NotStarted : MatchStatus.Paused;
                    break;
                case "next":
                case "nextperiod":
                    if (match.Status == MatchStatus.Running) throw new InvalidOperationException("比赛进行中不能直接进入下一节，请先暂停计时。");
                    if (match.CurrentPeriod >= match.PeriodCount) throw new InvalidOperationException("当前已经是最后一节。");
                    var previousPeriod = match.CurrentPeriod;
                    var previousRemaining = match.RemainingSeconds;
                    match.CurrentPeriod++;
                    match.RemainingSeconds = match.PeriodLengthSeconds;
                    match.IsClockRunning = false;
                    match.LastClockUpdateUtc = null;
                    match.Status = MatchStatus.Interval;
                    s.Data.Events.Add(new MatchEvent
                    {
                        MatchId = match.Id,
                        Side = TeamSide.Home,
                        Kind = MatchEventKind.ClockControl,
                        Period = match.CurrentPeriod,
                        ClockSecondsRemaining = match.RemainingSeconds,
                        Note = $"提前从第 {previousPeriod} 节进入第 {match.CurrentPeriod} 节，原剩余 {previousRemaining} 秒"
                    });
                    break;
                case "end":
                    match.IsClockRunning = false;
                    match.LastClockUpdateUtc = null;
                    match.Status = MatchStatus.Finished;
                    match.EndedAt ??= DateTime.UtcNow;
                    break;
                default:
                    throw new InvalidOperationException($"未知时钟操作：{action}");
            }
            s.Save();
            return Results.Ok(ApiMapping.ToScoreboard(match, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/scoreboard/{matchId:guid}/events", (Guid matchId, RecordEventRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(matchId) ?? throw new InvalidOperationException("比赛不存在。");
            s.UpdateClockFromElapsed(match);
            if (match.Status != MatchStatus.Running)
                throw new InvalidOperationException($"当前比赛状态为“{ApiMapping.StatusText(match.Status)}”，只有进行中才能记录比赛事件。");
            var roster = s.Data.Rosters.FirstOrDefault(item => item.MatchId == matchId && item.PlayerId == request.PlayerId && item.IsOnCourt)
                ?? throw new InvalidOperationException("该球员不在当前场上球员名单中。");
            var kind = ApiMapping.ParseEventKind(request.Kind);
            if (kind is MatchEventKind.TimeoutRequest)
                throw new InvalidOperationException("请使用暂停按钮记录暂停归属。");
            var points = kind == MatchEventKind.Score ? (request.Points is 1 or 2 or 3 ? request.Points : 2) : 0;
            var item = new MatchEvent
            {
                MatchId = matchId,
                PlayerId = request.PlayerId,
                Side = roster.Side,
                Kind = kind,
                Points = points,
                Period = match.CurrentPeriod,
                ClockSecondsRemaining = match.RemainingSeconds,
                Note = request.Note?.Trim() ?? ""
            };
            s.Data.Events.Add(item);
            s.Save();
            return Results.Ok(new { eventItem = ApiMapping.ToDto(item, s.Data), scoreboard = ApiMapping.ToScoreboard(match, s.Data) });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/scoreboard/{matchId:guid}/timeout", (Guid matchId, TimeoutAttributionRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(matchId) ?? throw new InvalidOperationException("比赛不存在。");
            s.UpdateClockFromElapsed(match);
            // Attribution can be recorded immediately after pause.
            if (match.Status is not (MatchStatus.Paused or MatchStatus.Interval or MatchStatus.Running))
                throw new InvalidOperationException("当前状态不能记录暂停归属。");

            // Ensure clock is paused when attributing from the pause button flow.
            if (match.Status == MatchStatus.Running)
            {
                match.IsClockRunning = false;
                match.LastClockUpdateUtc = null;
                match.Status = MatchStatus.Paused;
            }

            var mode = (request.Mode ?? "").Trim().ToLowerInvariant();
            MatchEvent item;
            if (mode is "other" or "none" or "neutral")
            {
                item = new MatchEvent
                {
                    MatchId = matchId,
                    PlayerId = null,
                    Side = TeamSide.Home,
                    Kind = MatchEventKind.ClockControl,
                    Period = match.CurrentPeriod,
                    ClockSecondsRemaining = match.RemainingSeconds,
                    Note = string.IsNullOrWhiteSpace(request.Note) ? "其它暂停" : request.Note.Trim()
                };
            }
            else if (mode is "team" or "team_other")
            {
                var side = ApiMapping.ParseSide(request.Side ?? "主队");
                item = new MatchEvent
                {
                    MatchId = matchId,
                    PlayerId = null,
                    Side = side,
                    Kind = MatchEventKind.TimeoutRequest,
                    Period = match.CurrentPeriod,
                    ClockSecondsRemaining = match.RemainingSeconds,
                    Note = string.IsNullOrWhiteSpace(request.Note) ? "球队暂停" : request.Note.Trim()
                };
            }
            else if (mode is "player")
            {
                if (request.PlayerId is null) throw new InvalidOperationException("请选择归属球员。");
                var roster = s.Data.Rosters.FirstOrDefault(r => r.MatchId == matchId && r.PlayerId == request.PlayerId)
                    ?? throw new InvalidOperationException("归属球员不在本场名单中。");
                item = new MatchEvent
                {
                    MatchId = matchId,
                    PlayerId = request.PlayerId,
                    Side = roster.Side,
                    Kind = MatchEventKind.TimeoutRequest,
                    Period = match.CurrentPeriod,
                    ClockSecondsRemaining = match.RemainingSeconds,
                    Note = string.IsNullOrWhiteSpace(request.Note) ? "申请暂停" : request.Note.Trim()
                };
            }
            else
            {
                throw new InvalidOperationException("未知暂停归属方式。");
            }

            s.Data.Events.Add(item);
            s.Save();
            return Results.Ok(new { eventItem = ApiMapping.ToDto(item, s.Data), scoreboard = ApiMapping.ToScoreboard(match, s.Data) });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/scoreboard/{matchId:guid}/substitutions", (Guid matchId, SubstitutionRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(matchId) ?? throw new InvalidOperationException("比赛不存在。");
            s.UpdateClockFromElapsed(match);
            if (match.Status != MatchStatus.Paused)
                throw new InvalidOperationException($"当前比赛状态为“{ApiMapping.StatusText(match.Status)}”，只有暂停中才能换人。");
            var outgoing = s.Data.Rosters.FirstOrDefault(item => item.MatchId == matchId && item.PlayerId == request.OutgoingPlayerId)
                ?? throw new InvalidOperationException("换下球员不在名单中。");
            var incoming = s.Data.Rosters.FirstOrDefault(item => item.MatchId == matchId && item.PlayerId == request.IncomingPlayerId)
                ?? throw new InvalidOperationException("换上球员不在名单中。");
            if (!outgoing.IsOnCourt) throw new InvalidOperationException("要换下的球员不在当前场上。");
            if (incoming.IsOnCourt || incoming.Side != outgoing.Side) throw new InvalidOperationException("换上球员必须是同一阵营的候补球员。");
            outgoing.IsOnCourt = false;
            incoming.IsOnCourt = true;
            var item = new MatchEvent
            {
                MatchId = matchId,
                PlayerId = request.OutgoingPlayerId,
                RelatedPlayerId = request.IncomingPlayerId,
                Side = outgoing.Side,
                Kind = MatchEventKind.Substitution,
                Period = match.CurrentPeriod,
                ClockSecondsRemaining = match.RemainingSeconds,
                Note = request.Note?.Trim() ?? ""
            };
            s.Data.Events.Add(item);
            s.Save();
            return Results.Ok(new { eventItem = ApiMapping.ToDto(item, s.Data), scoreboard = ApiMapping.ToScoreboard(match, s.Data) });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/matches/{id:guid}/events", (Guid id, AppSession session) => session.Read(s =>
{
    if (s.FindMatch(id) is null) return Results.NotFound(new ApiError("比赛不存在。"));
    var events = s.Data.Events.Where(item => item.MatchId == id)
        .OrderByDescending(item => item.CreatedAt)
        .Select(item => ApiMapping.ToDto(item, s.Data)).ToList();
    return Results.Ok(events);
}));

app.MapPost("/api/matches/{matchId:guid}/events/{eventId:guid}/void", (Guid matchId, Guid eventId, VoidEventRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var item = s.Data.Events.FirstOrDefault(e => e.Id == eventId && e.MatchId == matchId) ?? throw new InvalidOperationException("事件不存在。");
            if (item.IsVoided) throw new InvalidOperationException("该事件已经作废。");
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("作废事件必须填写原因。");
            item.IsVoided = true;
            item.VoidedAt = DateTime.UtcNow;
            item.VoidReason = request.Reason.Trim();
            item.VoidedBy = request.Operator?.Trim() ?? "";
            s.Save();
            return Results.Ok(ApiMapping.ToDto(item, s.Data));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapGet("/api/photos/{*relativePath}", (string relativePath, AppSession session) =>
{
    try
    {
        return session.Read(s =>
        {
            var cleaned = Uri.UnescapeDataString((relativePath ?? "").Split('?', 2)[0]).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var fullPath = s.Store.ResolvePath(cleaned);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            {
                // fallback: only file name under photos/
                var fileName = Path.GetFileName(cleaned);
                var photoPath = Path.Combine(s.Store.PhotoDirectory, fileName);
                if (!File.Exists(photoPath)) return Results.NotFound();
                fullPath = photoPath;
            }

            var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            return Results.File(fullPath, contentType);
        });
    }
    catch { return Results.NotFound(); }
});

app.MapGet("/api/settings/export-directory", (AppSession session) => session.Read(s => new { path = s.ExportDirectory }));

app.MapPut("/api/settings/export-directory", (ExportDirectoryRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            s.SetExportDirectory(request.Path.Trim());
            return Results.Ok(new { path = s.ExportDirectory });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/logs/export-csv", (ExportCsvRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(request.MatchId) ?? throw new InvalidOperationException("比赛不存在。");
            Directory.CreateDirectory(s.ExportDirectory);
            var safeName = string.Join("_", match.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "match";
            var matchDate = (match.ScheduledAt ?? DateTime.Now).ToLocalTime().ToString("yyyyMMdd");
            var exportPath = Path.Combine(s.ExportDirectory, $"{safeName}-{matchDate}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.csv");
            File.WriteAllText(exportPath, "﻿" + ExportHelpers.BuildMatchCsv(s.Data, match), System.Text.Encoding.UTF8);
            return Results.Ok(new { path = exportPath });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/logs/export-json", (ExportJsonRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var ids = request.MatchIds?.ToHashSet() ?? [];
            var matches = ids.Count == 0
                ? (request.MatchId is Guid one ? s.Data.Matches.Where(m => m.Id == one).ToList() : new List<Match>())
                : s.Data.Matches.Where(m => ids.Contains(m.Id)).ToList();
            if (matches.Count == 0) throw new InvalidOperationException("请选择要导出的比赛。");
            Directory.CreateDirectory(s.ExportDirectory);
            var batchDirectory = Path.Combine(s.ExportDirectory, "match-logs", $"{s.Workspace.Manifest.CompetitionId}-{DateTime.Now:yyyyMMdd-HHmmss-fff}");
            Directory.CreateDirectory(batchDirectory);
            var written = new List<string>();
            foreach (var match in matches)
            {
                var export = MatchLogService.Build(s.Data, s.Workspace.Manifest.CompetitionId, match);
                var fileName = $"matchlog-{s.Workspace.Manifest.CompetitionId}-{match.Id:N}-{DateTime.Now:yyyyMMddHHmmssfff}.json";
                var path = Path.Combine(batchDirectory, fileName);
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                written.Add(path);
            }
            return Results.Ok(new { directory = batchDirectory, files = written });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPost("/api/logs/import-json", async (HttpRequest request, AppSession session) =>
{
    try
    {
        if (!request.HasFormContentType) return Results.BadRequest(new ApiError("请上传 JSON 文件。"));
        var form = await request.ReadFormAsync();
        var files = form.Files.Where(f => f.Length > 0).ToList();
        if (files.Count == 0) return Results.BadRequest(new ApiError("未选择文件。"));

        return session.Write(s =>
        {
            var imported = 0;
            var skipped = 0;
            var rejected = new List<string>();
            foreach (var file in files)
            {
                try
                {
                    using var reader = new StreamReader(file.OpenReadStream());
                    var json = reader.ReadToEnd();
                    var export = System.Text.Json.JsonSerializer.Deserialize<MatchLogExport>(json);
                    if (export is null)
                    {
                        rejected.Add($"{file.FileName}：文件内容为空。");
                        continue;
                    }
                    var result = MatchLogService.Import(s.Data, export, s.Workspace.Manifest.CompetitionId);
                    if (result.Status == MatchLogImportStatus.Imported) imported++;
                    else if (result.Status == MatchLogImportStatus.Skipped) skipped++;
                    else rejected.Add($"{file.FileName}：{result.Message}");
                }
                catch (Exception ex)
                {
                    rejected.Add($"{file.FileName}：{ex.Message}");
                }
            }
            if (imported > 0) s.Save();
            return Results.Ok(new { imported, skipped, rejected });
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

app.MapPut("/api/matches/{matchId:guid}/roster/{rosterId:guid}", (Guid matchId, Guid rosterId, UpdateRosterRequest request, AppSession session) =>
{
    try
    {
        return session.Write(s =>
        {
            var match = s.FindMatch(matchId) ?? throw new InvalidOperationException("比赛不存在。");
            var roster = s.Data.Rosters.FirstOrDefault(item => item.Id == rosterId && item.MatchId == matchId)
                ?? throw new InvalidOperationException("名单记录不存在。");
            var player = s.Data.Players.FirstOrDefault(item => item.Id == request.PlayerId)
                ?? throw new InvalidOperationException("球员不存在。");
            var side = ApiMapping.ParseSide(request.Side);
            var sideTeamId = side == TeamSide.Home ? match.HomeTeamId : match.AwayTeamId;
            if (player.TeamId is null || sideTeamId is null || player.TeamId != sideTeamId)
                throw new InvalidOperationException("正式比赛名单只能选择所选阵营队伍下的球员。");
            if (s.Data.Rosters.Any(item => item.MatchId == matchId && item.PlayerId == player.Id && item.Id != rosterId))
                throw new InvalidOperationException("该球员已在本场比赛名单中。");
            if (roster.IsOnCourt && match.Status != MatchStatus.NotStarted && (roster.PlayerId != player.Id || roster.Side != side))
                throw new InvalidOperationException("已在场球员不能通过名单编辑直接改人或改阵营，请在暂停状态使用换人。");

            var jersey = request.JerseyNumber.Trim();
            if (!string.IsNullOrWhiteSpace(jersey)
                && s.Data.Rosters.Any(item => item.MatchId == matchId && item.Side == side && item.Id != rosterId && item.JerseyNumber.Equals(jersey, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"该阵营已存在 {jersey} 号球员。");

            if (match.Status == MatchStatus.NotStarted
                && request.IsStarter
                && s.Data.Rosters.Count(item => item.MatchId == matchId && item.Side == side && item.Id != rosterId && item.IsStarter) >= 5)
                throw new InvalidOperationException("同一阵营首发不能超过 5 人。");

            if (match.Status != MatchStatus.NotStarted)
                PlayerOps.EnsureAudit(request.AuditOperator, request.AuditNote, match);

            var before = $"{ApiMapping.SideText(roster.Side)} {roster.JerseyNumber}";
            roster.PlayerId = player.Id;
            roster.Side = side;
            roster.JerseyNumber = jersey;
            roster.IsStarter = request.IsStarter;
            if (match.Status == MatchStatus.NotStarted)
            {
                roster.IsOnCourt = request.IsStarter;
            }

            if (match.Status != MatchStatus.NotStarted)
            {
                PlayerOps.AddRosterAudit(s, match, side, request.AuditOperator!, request.AuditNote!,
                    $"更新名单：{before} -> {ApiMapping.SideText(side)} {player.DisplayName} {jersey}");
            }

            s.Save();
            return Results.Ok(ApiMapping.ToDto(roster, s.Data, match));
        });
    }
    catch (Exception ex) { return Results.BadRequest(new ApiError(ex.Message)); }
});

if (webRoot is not null && Directory.Exists(webRoot))
{
    var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot);
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
}

app.Run();

static string? ResolveWebRoot(params string[] roots)
{
    foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
    {
        var candidates = new[]
        {
            Path.Combine(root, "wwwroot"),
            Path.GetFullPath(Path.Combine(root, @"..\..\..\wwwroot")),
            Path.GetFullPath(Path.Combine(root, @"..\..\..\..\BasketballManager.Api\wwwroot")),
            Path.GetFullPath(Path.Combine(root, @"..\..\..\BasketballManager.WebUI\dist")),
            Path.GetFullPath(Path.Combine(root, @"..\..\..\..\BasketballManager.WebUI\dist")),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }
        }
    }

    return null;
}

public partial class Program;

internal sealed record ExportDirectoryRequest(string Path);
internal sealed record ExportCsvRequest(Guid MatchId);
internal sealed record ExportJsonRequest(Guid? MatchId, List<Guid>? MatchIds);
internal sealed record UpdateRosterRequest(Guid PlayerId, string Side, string JerseyNumber, bool IsStarter, string? AuditOperator, string? AuditNote);
internal sealed record TimeoutAttributionRequest(string Mode, string? Side, Guid? PlayerId, string? Note);

internal static class ExportHelpers
{
    public static string BuildMatchCsv(AppData data, Match match)
    {
        var projection = Statistics.Compute(data, match);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"比赛名称,{Escape(match.Name)}");
        sb.AppendLine($"主队,{Escape(match.HomeTeamName)}");
        sb.AppendLine($"客队,{Escape(match.AwayTeamName)}");
        sb.AppendLine($"状态,{Escape(ApiMapping.StatusText(match.Status))}");
        sb.AppendLine($"比分,{projection.HomeScore}-{projection.AwayScore}");
        sb.AppendLine();
        sb.AppendLine("事件流水");
        sb.AppendLine("时间,节次,剩余秒,队伍,球员,事件,分值,备注,作废");
        foreach (var item in data.Events.Where(e => e.MatchId == match.Id).OrderBy(e => e.CreatedAt))
        {
            var player = item.PlayerId is null ? "" : data.Players.FirstOrDefault(p => p.Id == item.PlayerId)?.DisplayName ?? "";
            sb.AppendLine(string.Join(',',
                item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                item.Period,
                item.ClockSecondsRemaining,
                Escape(ApiMapping.SideText(item.Side)),
                Escape(player),
                Escape(ApiMapping.KindText(item.Kind)),
                item.Points,
                Escape(item.Note),
                item.IsVoided ? "是" : "否"));
        }
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

internal static class PlayerOps
{
    public static Player Upsert(AppSession session, Guid? id, string name, string studentNumber, Guid? teamId, string? note, string? status, Dictionary<string, string>? customFields)
    {
        name = name.Trim();
        studentNumber = studentNumber.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("球员姓名不能为空。");
        if (session.Data.Players.Any(player => player.Id != id && !string.IsNullOrWhiteSpace(studentNumber)
            && player.StudentNumber.Equals(studentNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("学号已存在。");

        Player player;
        if (id is null)
        {
            player = new Player();
            session.Data.Players.Add(player);
        }
        else
        {
            player = session.Data.Players.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("球员不存在。");
        }

        var team = teamId is null ? null : session.Data.Teams.FirstOrDefault(item => item.Id == teamId);
        player.Name = name;
        player.StudentNumber = studentNumber;
        player.TeamId = team?.Id;
        player.Team = team?.Name ?? "";
        player.Note = note?.Trim() ?? "";
        player.Status = string.IsNullOrWhiteSpace(status) ? "在队" : status.Trim();
        player.UpdatedAt = DateTime.UtcNow;

        if (customFields is not null)
        {
            foreach (var pair in customFields)
            {
                var field = session.Data.PlayerFields.FirstOrDefault(item => item.Name == pair.Key);
                if (field is null) continue;
                var value = session.Data.PlayerFieldValues.FirstOrDefault(item => item.PlayerId == player.Id && item.FieldId == field.Id);
                if (value is null)
                {
                    session.Data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = field.Id, Value = pair.Value?.Trim() ?? "" });
                }
                else value.Value = pair.Value?.Trim() ?? "";
            }
        }
        return player;
    }

    public static void EnsureAudit(string? auditOperator, string? auditNote, Match match)
    {
        if (string.IsNullOrWhiteSpace(auditOperator) || string.IsNullOrWhiteSpace(auditNote))
            throw new InvalidOperationException($"当前比赛状态为“{ApiMapping.StatusText(match.Status)}”，修改名单需填写审计操作人与备注。");
    }

    public static void AddRosterAudit(AppSession session, Match match, TeamSide side, string auditOperator, string auditNote, string operation)
    {
        session.Data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            Side = side,
            Kind = MatchEventKind.RosterAudit,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds,
            Note = $"名单审计修改：{operation}；操作人：{auditOperator}；备注：{auditNote}"
        });
    }

    public static void EnsureCanRunClock(Match match, AppSession session)
    {
        if (match.Status == MatchStatus.Finished) throw new InvalidOperationException("比赛已结束，不能继续计时。");
        if (match.RemainingSeconds <= 0) throw new InvalidOperationException("本节时间已经结束，请进入下一节或结束比赛。");
        if (match.Status == MatchStatus.NotStarted)
        {
            foreach (var roster in session.Data.Rosters.Where(item => item.MatchId == match.Id))
                roster.IsOnCourt = roster.IsStarter;
        }
        var homeRoster = session.Data.Rosters.Count(item => item.MatchId == match.Id && item.Side == TeamSide.Home);
        var awayRoster = session.Data.Rosters.Count(item => item.MatchId == match.Id && item.Side == TeamSide.Away);
        if (homeRoster == 0 || awayRoster == 0) throw new InvalidOperationException("开始比赛前主队和客队名单都至少需要 1 名球员。");
        var homeOnCourt = session.Data.Rosters.Count(item => item.MatchId == match.Id && item.Side == TeamSide.Home && item.IsOnCourt);
        var awayOnCourt = session.Data.Rosters.Count(item => item.MatchId == match.Id && item.Side == TeamSide.Away && item.IsOnCourt);
        if (homeOnCourt == 0 || awayOnCourt == 0) throw new InvalidOperationException("开始比赛前双方都至少需要 1 名首发/场上球员。");
        if (homeOnCourt > 5 || awayOnCourt > 5) throw new InvalidOperationException("同一阵营场上球员不能超过 5 人。");
    }
}
