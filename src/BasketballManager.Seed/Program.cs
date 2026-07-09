using BasketballManager;

// Seed a complete competition workspace: test001
// Usage: dotnet run --project src/BasketballManager.Seed

var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BasketballManager");
var manager = new CompetitionWorkspaceManager(appDir);

CompetitionWorkspace workspace;
var existing = manager.ListImportedWorkspaces()
    .FirstOrDefault(item => item.Manifest.CompetitionId.Equals("test001", StringComparison.OrdinalIgnoreCase));
if (existing is not null)
{
    var storeExisting = new DataStore(existing.DirectoryPath);
    storeExisting.Save(new AppData { SchemaVersion = 6 });
    workspace = existing;
    manager.SaveCurrentWorkspace(workspace.DirectoryPath);
    Console.WriteLine("Reusing existing competition test001 and resetting data.");
}
else
{
    workspace = manager.CreateCompetition("测试完整赛事 test001", "test001");
    manager.SaveCurrentWorkspace(workspace.DirectoryPath);
    Console.WriteLine("Created competition test001.");
}

var store = new DataStore(workspace.DirectoryPath);
var data = new AppData { SchemaVersion = 6 };

var fieldPos = new PlayerFieldDefinition { Name = "位置", FieldType = "Text", DisplayOrder = 0 };
var fieldHeight = new PlayerFieldDefinition { Name = "身高", FieldType = "Text", DisplayOrder = 1 };
var fieldGrade = new PlayerFieldDefinition { Name = "年级", FieldType = "Text", DisplayOrder = 2 };
data.PlayerFields.AddRange([fieldPos, fieldHeight, fieldGrade]);

var home = new Team { Name = "计算机学院", Note = "主队样例", Status = "启用" };
var away = new Team { Name = "经济管理学院", Note = "客队样例", Status = "启用" };
data.Teams.AddRange([home, away]);

Player MakePlayer(string name, string sno, Team team, string pos, string height, string grade, bool withPhoto)
{
    var player = new Player
    {
        Name = name,
        StudentNumber = sno,
        TeamId = team.Id,
        Team = team.Name,
        Status = "在队",
        Note = $"{team.Name} 样例球员"
    };
    data.Players.Add(player);
    data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = fieldPos.Id, Value = pos });
    data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = fieldHeight.Id, Value = height });
    data.PlayerFieldValues.Add(new PlayerFieldValue { PlayerId = player.Id, FieldId = fieldGrade.Id, Value = grade });
    if (withPhoto)
    {
        player.PhotoPath = CreatePhoto(store, name);
    }
    return player;
}

var h1 = MakePlayer("陈思远", "T20230001", home, "得分后卫", "186cm", "大三", true);
var h2 = MakePlayer("李明轩", "T20230002", home, "控球后卫", "178cm", "大二", true);
var h3 = MakePlayer("赵一诺", "T20230003", home, "小前锋", "190cm", "大一", false);
var h4 = MakePlayer("周子墨", "T20230004", home, "中锋", "196cm", "大四", true);
var h5 = MakePlayer("孙启航", "T20230005", home, "大前锋", "192cm", "大三", false);
var h6 = MakePlayer("林晓博", "T20230006", home, "替补前锋", "188cm", "大二", false);

var a1 = MakePlayer("韩宇泽", "T20221001", away, "得分后卫", "184cm", "大四", true);
var a2 = MakePlayer("吴佳怡", "T20221002", away, "控球后卫", "172cm", "大二", false);
var a3 = MakePlayer("郑浩然", "T20221003", away, "中锋", "198cm", "大三", true);
var a4 = MakePlayer("高子涵", "T20221004", away, "小前锋", "185cm", "大一", false);
var a5 = MakePlayer("唐启铭", "T20221005", away, "大前锋", "191cm", "大二", false);
var a6 = MakePlayer("何子安", "T20221006", away, "替补后卫", "180cm", "大三", false);

var match = new Match
{
    Name = "半决赛 A",
    HomeTeamId = home.Id,
    AwayTeamId = away.Id,
    HomeTeamName = home.Name,
    AwayTeamName = away.Name,
    ScheduledAt = DateTime.UtcNow.Date.AddHours(15),
    Location = "体育馆 A 场",
    Note = "test001 完整样例比赛",
    PeriodCount = 4,
    PeriodLengthSeconds = 600,
    CurrentPeriod = 2,
    RemainingSeconds = 462,
    Status = MatchStatus.Paused,
    IsClockRunning = false
};
data.Matches.Add(match);

void AddRoster(Player p, TeamSide side, string jersey, bool starter, bool onCourt)
{
    data.Rosters.Add(new MatchRoster
    {
        MatchId = match.Id,
        PlayerId = p.Id,
        Side = side,
        JerseyNumber = jersey,
        IsStarter = starter,
        IsOnCourt = onCourt
    });
}

AddRoster(h1, TeamSide.Home, "7", true, true);
AddRoster(h2, TeamSide.Home, "3", true, true);
AddRoster(h3, TeamSide.Home, "11", true, true);
AddRoster(h4, TeamSide.Home, "21", true, true);
AddRoster(h5, TeamSide.Home, "9", true, true);
AddRoster(h6, TeamSide.Home, "12", false, false);
AddRoster(a1, TeamSide.Away, "5", true, true);
AddRoster(a2, TeamSide.Away, "8", true, true);
AddRoster(a3, TeamSide.Away, "14", true, true);
AddRoster(a4, TeamSide.Away, "1", true, true);
AddRoster(a5, TeamSide.Away, "24", true, true);
AddRoster(a6, TeamSide.Away, "17", false, false);

void AddEvent(Player? player, TeamSide side, MatchEventKind kind, int points, int period, int clock, string note = "", bool voided = false)
{
    data.Events.Add(new MatchEvent
    {
        MatchId = match.Id,
        PlayerId = player?.Id,
        Side = side,
        Kind = kind,
        Points = points,
        Period = period,
        ClockSecondsRemaining = clock,
        Note = note,
        IsVoided = voided,
        VoidedAt = voided ? DateTime.UtcNow : null,
        VoidReason = voided ? "录入错误" : "",
        VoidedBy = voided ? "记分员小王" : "",
        CreatedAt = DateTime.UtcNow.AddMinutes(-30 + data.Events.Count)
    });
}

AddEvent(h1, TeamSide.Home, MatchEventKind.Score, 2, 1, 580, "快攻上篮");
AddEvent(a1, TeamSide.Away, MatchEventKind.Score, 3, 1, 560, "弧顶三分");
AddEvent(h2, TeamSide.Home, MatchEventKind.Assist, 0, 1, 559);
AddEvent(a3, TeamSide.Away, MatchEventKind.Rebound, 0, 1, 540);
AddEvent(h4, TeamSide.Home, MatchEventKind.Score, 2, 1, 500, "内线强打");
AddEvent(a2, TeamSide.Away, MatchEventKind.Foul, 0, 1, 490, "阻挡");
AddEvent(h3, TeamSide.Home, MatchEventKind.Steal, 0, 1, 470);
AddEvent(a4, TeamSide.Away, MatchEventKind.Turnover, 0, 1, 468);
AddEvent(null, TeamSide.Home, MatchEventKind.ClockControl, 0, 1, 0, "第1节结束");
AddEvent(h1, TeamSide.Home, MatchEventKind.Score, 3, 2, 590, "底角三分");
AddEvent(a1, TeamSide.Away, MatchEventKind.Score, 2, 2, 570);
AddEvent(h4, TeamSide.Home, MatchEventKind.Block, 0, 2, 550);
AddEvent(a3, TeamSide.Away, MatchEventKind.Score, 2, 2, 530, "二次进攻");
AddEvent(h2, TeamSide.Home, MatchEventKind.TimeoutRequest, 0, 2, 500, "申请暂停");
AddEvent(null, TeamSide.Home, MatchEventKind.ClockControl, 0, 2, 500, "暂停计时");
AddEvent(h5, TeamSide.Home, MatchEventKind.Foul, 0, 2, 480);
AddEvent(a2, TeamSide.Away, MatchEventKind.Score, 1, 2, 479, "罚球");

data.Events.Add(new MatchEvent
{
    MatchId = match.Id,
    PlayerId = h5.Id,
    RelatedPlayerId = h6.Id,
    Side = TeamSide.Home,
    Kind = MatchEventKind.Substitution,
    Period = 2,
    ClockSecondsRemaining = 470,
    Note = "换人：孙启航 -> 林晓博",
    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
});
data.Rosters.First(r => r.PlayerId == h5.Id).IsOnCourt = false;
data.Rosters.First(r => r.PlayerId == h6.Id).IsOnCourt = true;

AddEvent(h1, TeamSide.Home, MatchEventKind.Score, 2, 2, 450);
AddEvent(a5, TeamSide.Away, MatchEventKind.Rebound, 0, 2, 448);
AddEvent(h3, TeamSide.Home, MatchEventKind.Turnover, 0, 1, 120, "已作废示例", voided: true);

store.Save(data);
Console.WriteLine($"Seeded competition: {workspace.Manifest.Name} ({workspace.Manifest.CompetitionId})");
Console.WriteLine($"Workspace: {workspace.DirectoryPath}");
Console.WriteLine($"Players: {data.Players.Count}, Events: {data.Events.Count}");
Console.WriteLine("Open modern UI and switch to competition ID test001.");
return 0;

static string CreatePhoto(DataStore store, string name)
{
    Directory.CreateDirectory(store.PhotoDirectory);
    var fileName = $"{Guid.NewGuid():N}.png";
    var full = Path.Combine(store.PhotoDirectory, fileName);

    // Prefer project logo if present; otherwise write a tiny valid PNG.
    var logoCandidates = new[]
    {
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Frontend materials\logo.png")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"Frontend materials\logo.png")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\Frontend materials\logo.png")),
    };
    var logo = logoCandidates.FirstOrDefault(File.Exists);
    if (logo is not null)
    {
        File.Copy(logo, full, overwrite: true);
    }
    else
    {
        // 1x1 blue PNG
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5W1Z8AAAAASUVORK5CYII=");
        File.WriteAllBytes(full, png);
    }

    _ = name;
    return Path.Combine("photos", fileName);
}
