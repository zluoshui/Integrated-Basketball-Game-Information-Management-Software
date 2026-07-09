using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BasketballManager;

public partial class MainWindow : Window
{
    private readonly CompetitionWorkspaceManager _workspaceManager = new();
    private readonly DispatcherTimer _timer = new();
    private DataStore _store = null!;
    private CompetitionWorkspace _workspace = null!;
    private AppData _data = new();
    private string _exportDirectory = "";
    private bool _isRefreshingCompetitionList;
    private bool _isRefreshingMatchHistory;
    private Player? _selectedOnCourtPlayer;

    public MainWindow()
    {
        InitializeComponent();

        _workspace = OpenInitialWorkspace();
        _store = new DataStore(_workspace.DirectoryPath);
        _data = LoadData();
        _exportDirectory = LoadExportDirectory();
        RefreshCompetitionWorkspace();
        RefreshExportDirectoryText();
        PauseRunningMatchesAfterRestart();
        PlayerStatusCombo.ItemsSource = new[] { "在队", "离队", "停用" };
        PlayerStatusCombo.SelectedItem = "在队";
        RosterSideCombo.ItemsSource = Enum.GetValues<TeamSide>();
        RosterSideCombo.SelectedItem = TeamSide.Home;
        RosterSideCombo.SelectionChanged += (_, _) => RefreshRosterCandidates();
        EventFilterSideCombo.ItemsSource = new[] { "全部", "主队", "客队" };
        EventFilterSideCombo.SelectedIndex = 0;
        EventFilterKindCombo.ItemsSource = new object[] { "全部" }.Concat(Enum.GetValues<MatchEventKind>().Cast<object>()).ToList();
        EventFilterKindCombo.SelectedIndex = 0;

        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) =>
        {
            UpdateClockFromElapsed();
            RefreshScoreboard();
        };

        RefreshAll();
        _timer.Start();
    }

    private CompetitionWorkspace OpenInitialWorkspace()
    {
        try
        {
            return _workspaceManager.OpenOrCreateInitialWorkspace();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "赛事工作区初始化失败", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private void SwitchWorkspace(CompetitionWorkspace workspace, bool saveCurrent = true)
    {
        if (saveCurrent)
        {
            UpdateClockFromElapsed();
            SaveData();
        }
        _workspace = workspace;
        _store = new DataStore(_workspace.DirectoryPath);
        _data = LoadData();
        _exportDirectory = LoadExportDirectory();
        RefreshCompetitionWorkspace();
        RefreshExportDirectoryText();
        PauseRunningMatchesAfterRestart();
        RefreshAll();
    }

    private void RefreshCompetitionWorkspace()
    {
        CompetitionNameBox.Text = _workspace.Manifest.Name;
        CompetitionIdBox.Text = _workspace.Manifest.CompetitionId;
        CompetitionPathText.Text = $"赛事目录：{_workspace.DirectoryPath}";
        RefreshImportedCompetitionList();
    }

    private void RefreshImportedCompetitionList()
    {
        _isRefreshingCompetitionList = true;
        try
        {
            var workspaces = _workspaceManager.ListImportedWorkspaces();
            CompetitionListCombo.ItemsSource = workspaces;
            CompetitionListCombo.SelectedItem = workspaces.FirstOrDefault(item => item.DirectoryPath.Equals(_workspace.DirectoryPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isRefreshingCompetitionList = false;
        }
    }

    private AppData LoadData()
    {
        try
        {
            return _store.Load();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "读取失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return new AppData();
        }
    }

    private void SaveData()
    {
        try
        {
            _store.Save(_data);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Match? PrepMatch => MatchCombo.SelectedItem as Match;
    private Match? ScoreboardMatch => ScoreboardMatchCombo.SelectedItem as Match;
    private Match? LogMatch =>
        MatchesHistoryGrid.SelectedItem is MatchHistoryRow row
            ? FindMatch(row.比赛ID)
            : null;
    private Player? SelectedPlayer => PlayersGrid.SelectedItem as Player;
    private Team? SelectedTeam => TeamsGrid.SelectedItem as Team;
    private Player? ActivePlayer => _selectedOnCourtPlayer;
    private RosterRow? SelectedRoster => RostersGrid.SelectedItem as RosterRow;
    private EventRow? SelectedEvent => EventsGrid.SelectedItem as EventRow;
    private IReadOnlyList<MatchHistoryRow> CheckedMatchHistoryRows => (MatchesHistoryGrid.ItemsSource as IEnumerable<MatchHistoryRow>)?.Where(row => row.导出).ToList() ?? [];

    private Match? FindMatch(Guid? id) =>
        id is null || id == Guid.Empty ? null : _data.Matches.FirstOrDefault(match => match.Id == id.Value);

    private void PreserveComboSelection(ComboBox combo, Guid? preferredId)
    {
        var matches = (combo.ItemsSource as IEnumerable<Match>)?.ToList() ?? [];
        combo.SelectedItem = matches.FirstOrDefault(match => match.Id == preferredId) ?? matches.FirstOrDefault();
    }

    private void RefreshAll()
    {
        RefreshTeams();
        RefreshPlayers();
        RefreshMatches();
        RefreshRosters();
        RefreshScoreboard();
        RefreshEvents();
    }

    private void RefreshPlayers()
    {
        var search = PlayerSearchBox.Text.Trim();
        var players = _data.Players
            .Where(player => string.IsNullOrWhiteSpace(search)
                || player.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || player.StudentNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || player.Team.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.Team)
            .ThenBy(player => player.Name)
            .ToList();

        PlayersGrid.ItemsSource = players;
        PlayerTeamCombo.ItemsSource = _data.Teams.Where(team => team.Status == "启用").OrderBy(team => team.Name).ToList();
        RefreshRosterCandidates();
        RefreshCustomFields();
        RefreshActivePlayers();
    }

    private void RefreshTeams()
    {
        var teams = _data.Teams.OrderBy(team => team.Status).ThenBy(team => team.Name).ToList();
        TeamsGrid.ItemsSource = teams;
        PlayerTeamCombo.ItemsSource = _data.Teams.Where(team => team.Status == "启用").OrderBy(team => team.Name).ToList();
        HomeTeamCombo.ItemsSource = _data.Teams.Where(team => team.Status == "启用").OrderBy(team => team.Name).ToList();
        AwayTeamCombo.ItemsSource = _data.Teams.Where(team => team.Status == "启用").OrderBy(team => team.Name).ToList();
    }

    private void RefreshCustomFields()
    {
        var player = SelectedPlayer;
        CustomFieldsGrid.ItemsSource = _data.PlayerFields
            .OrderBy(field => field.DisplayOrder)
            .Select(field => new
            {
                字段 = field.Name,
                值 = player is null
                    ? ""
                    : _data.PlayerFieldValues.FirstOrDefault(value => value.PlayerId == player.Id && value.FieldId == field.Id)?.Value ?? ""
            })
            .ToList();
    }

    private void RefreshMatches()
    {
        var prepSelectedId = PrepMatch?.Id;
        var scoreboardSelectedId = ScoreboardMatch?.Id;
        var orderedMatches = _data.Matches.OrderByDescending(match => match.CreatedAt).ToList();
        MatchCombo.ItemsSource = orderedMatches;
        ScoreboardMatchCombo.ItemsSource = orderedMatches;
        PreserveComboSelection(MatchCombo, prepSelectedId);
        PreserveComboSelection(ScoreboardMatchCombo, scoreboardSelectedId);
        RefreshMatchHistory();
    }

    private void RefreshMatchHistory()
    {
        var checkedIds = CheckedMatchHistoryRows.Select(row => row.比赛ID).ToHashSet();
        var selectedLogId = LogMatch?.Id;
        _isRefreshingMatchHistory = true;
        try
        {
            var rows = _data.Matches
                .OrderByDescending(match => match.CreatedAt)
                .Select(match => new MatchHistoryRow
                {
                    导出 = checkedIds.Contains(match.Id),
                    比赛ID = match.Id,
                    创建时间 = match.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    比赛名 = match.Name,
                    主队 = match.HomeTeamName,
                    客队 = match.AwayTeamName,
                    状态 = MatchStatusText(match.Status),
                    事件数 = _data.Events.Count(item => item.MatchId == match.Id),
                    名单数 = _data.Rosters.Count(item => item.MatchId == match.Id)
                })
                .ToList();
            MatchesHistoryGrid.ItemsSource = rows;
            MatchesHistoryGrid.SelectedItem = rows.FirstOrDefault(row => row.比赛ID == selectedLogId) ?? rows.FirstOrDefault();
        }
        finally
        {
            _isRefreshingMatchHistory = false;
        }
    }

    private void RefreshRosters()
    {
        var match = PrepMatch;
        if (match is null)
        {
            RostersGrid.ItemsSource = null;
            return;
        }

        RostersGrid.ItemsSource = _data.Rosters
            .Where(roster => roster.MatchId == match.Id)
            .Select(roster =>
            {
                var player = _data.Players.FirstOrDefault(item => item.Id == roster.PlayerId);
                return new RosterRow
                {
                    名单ID = roster.Id,
                    球员 = player?.DisplayName ?? "未知球员",
                    队伍 = roster.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName,
                    号码 = roster.JerseyNumber,
                    首发 = roster.IsStarter,
                    在场 = roster.IsOnCourt
                };
            })
            .ToList();

        RefreshRosterCandidates();
    }

    private void RefreshRosterCandidates()
    {
        var match = PrepMatch;
        var side = RosterSideCombo.SelectedItem is TeamSide selectedSide ? selectedSide : TeamSide.Home;
        var sideTeamId = side == TeamSide.Home ? match?.HomeTeamId : match?.AwayTeamId;
        RosterPlayerCombo.ItemsSource = _data.Players
            .Where(player => player.Status != "停用")
            .Where(player => sideTeamId is null || player.TeamId == sideTeamId)
            .OrderBy(player => player.Team)
            .ThenBy(player => player.Name)
            .ToList();
    }

    private void RefreshActivePlayers()
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            HomeOnCourtList.ItemsSource = null;
            AwayOnCourtList.ItemsSource = null;
            SubstitutePlayerCombo.ItemsSource = null;
            SelectedOnCourtPlayerText.Text = "未选择场上球员";
            _selectedOnCourtPlayer = null;
            return;
        }

        var selectedId = _selectedOnCourtPlayer?.Id;
        var homePlayers = GetOnCourtPlayers(match, TeamSide.Home);
        var awayPlayers = GetOnCourtPlayers(match, TeamSide.Away);
        HomeOnCourtTitle.Text = $"{match.HomeTeamName} 场上球员";
        AwayOnCourtTitle.Text = $"{match.AwayTeamName} 场上球员";
        HomeOnCourtList.ItemsSource = homePlayers;
        AwayOnCourtList.ItemsSource = awayPlayers;
        _selectedOnCourtPlayer = homePlayers.Concat(awayPlayers).FirstOrDefault(player => player.Id == selectedId);
        HomeOnCourtList.SelectedItem = homePlayers.FirstOrDefault(player => player.Id == _selectedOnCourtPlayer?.Id);
        AwayOnCourtList.SelectedItem = awayPlayers.FirstOrDefault(player => player.Id == _selectedOnCourtPlayer?.Id);
        RefreshSubstituteCandidates();
        RefreshSelectedOnCourtText();
    }

    private List<Player> GetOnCourtPlayers(Match match, TeamSide side)
    {
        return _data.Rosters
            .Where(roster => roster.MatchId == match.Id && roster.Side == side && roster.IsOnCourt)
            .Select(roster => _data.Players.FirstOrDefault(player => player.Id == roster.PlayerId))
            .Where(player => player is not null)
            .OrderBy(player => player!.Team)
            .ThenBy(player => player!.Name)
            .Cast<Player>()
            .ToList();
    }

    private void RefreshSubstituteCandidates()
    {
        var match = ScoreboardMatch;
        var selected = _selectedOnCourtPlayer;
        if (match is null || selected is null)
        {
            SubstitutePlayerCombo.ItemsSource = null;
            return;
        }

        var selectedRoster = GetRosterForPlayer(match, selected);
        if (selectedRoster is null)
        {
            SubstitutePlayerCombo.ItemsSource = null;
            return;
        }

        SubstitutePlayerCombo.ItemsSource = _data.Rosters
            .Where(roster => roster.MatchId == match.Id && roster.Side == selectedRoster.Side && !roster.IsOnCourt)
            .Select(roster => _data.Players.FirstOrDefault(player => player.Id == roster.PlayerId))
            .Where(player => player is not null)
            .OrderBy(player => player!.Team)
            .ThenBy(player => player!.Name)
            .Cast<Player>()
            .ToList();
    }

    private void RefreshSelectedOnCourtText()
    {
        SelectedOnCourtPlayerText.Text = _selectedOnCourtPlayer is null
            ? "未选择场上球员"
            : $"当前记录球员：{_selectedOnCourtPlayer.DisplayName}";
    }

    private void RefreshScoreboard()
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            TimerText.Text = "00:00";
            ScoreText.Text = "0 - 0";
            PeriodText.Text = "未选择比赛";
            StatusText.Text = "无比赛";
            FoulTimeoutText.Text = "0:0 / 0:0";
            StatsGrid.ItemsSource = null;
            RefreshScoreboardControls(null);
            RefreshActivePlayers();
            return;
        }

        var projection = Statistics.Compute(_data, match);
        TimerText.Text = FormatSeconds(match.RemainingSeconds);
        ScoreText.Text = $"{projection.HomeScore} - {projection.AwayScore}";
        PeriodText.Text = $"第 {match.CurrentPeriod} / {match.PeriodCount} 节";
        StatusText.Text = MatchStatusText(match.Status);
        var currentPeriodStats = projection.PeriodTeamStats.FirstOrDefault(item => item.Period == match.CurrentPeriod);
        FoulTimeoutText.Text = $"全场犯规 {projection.HomeFouls}:{projection.AwayFouls} / 暂停 {projection.HomeTimeouts}:{projection.AwayTimeouts} / 本节犯规 {currentPeriodStats?.HomeFouls ?? 0}:{currentPeriodStats?.AwayFouls ?? 0}";
        StatsGrid.ItemsSource = projection.PlayerStats;
        RefreshScoreboardControls(match);
        RefreshActivePlayers();
    }

    private void RefreshScoreboardControls(Match? match)
    {
        if (match is null)
        {
            StartClockButton.IsEnabled = false;
            ResumeClockButton.IsEnabled = false;
            PauseClockButton.IsEnabled = false;
            ResetClockButton.IsEnabled = false;
            NextPeriodButton.IsEnabled = false;
            EndMatchButton.IsEnabled = false;
            OnCourtPanel.IsEnabled = false;
            SubstitutionPanel.IsEnabled = false;
            EventActionsPanel.IsEnabled = false;
            return;
        }

        var isFinished = match.Status == MatchStatus.Finished;
        var isRunning = match.Status == MatchStatus.Running;
        var canStart = !isFinished && !isRunning && match.RemainingSeconds > 0;

        StartClockButton.IsEnabled = canStart && match.Status is MatchStatus.NotStarted or MatchStatus.Interval;
        ResumeClockButton.IsEnabled = canStart && match.Status == MatchStatus.Paused;
        PauseClockButton.IsEnabled = isRunning;
        ResetClockButton.IsEnabled = !isFinished;
        NextPeriodButton.IsEnabled = !isFinished && !isRunning && match.CurrentPeriod < match.PeriodCount;
        EndMatchButton.IsEnabled = !isFinished;
        OnCourtPanel.IsEnabled = !isFinished;
        SubstitutionPanel.IsEnabled = match.Status == MatchStatus.Paused;
        EventActionsPanel.IsEnabled = isRunning;
    }

    private void RefreshEvents()
    {
        var match = LogMatch;
        if (match is null)
        {
            TeamStatsGrid.ItemsSource = null;
            EventsGrid.ItemsSource = null;
            return;
        }

        var projection = Statistics.Compute(_data, match);
        TeamStatsGrid.ItemsSource = new[]
        {
            new
            {
                队伍 = match.HomeTeamName,
                比分 = projection.HomeScore,
                犯规 = projection.HomeFouls,
                暂停 = projection.HomeTimeouts,
                本节犯规 = projection.PeriodTeamStats.FirstOrDefault(item => item.Period == match.CurrentPeriod)?.HomeFouls ?? 0,
                篮板 = projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Rebounds),
                助攻 = projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Assists),
                抢断 = projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Steals),
                盖帽 = projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Blocks),
                失误 = projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Turnovers)
            },
            new
            {
                队伍 = match.AwayTeamName,
                比分 = projection.AwayScore,
                犯规 = projection.AwayFouls,
                暂停 = projection.AwayTimeouts,
                本节犯规 = projection.PeriodTeamStats.FirstOrDefault(item => item.Period == match.CurrentPeriod)?.AwayFouls ?? 0,
                篮板 = projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Rebounds),
                助攻 = projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Assists),
                抢断 = projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Steals),
                盖帽 = projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Blocks),
                失误 = projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Turnovers)
            }
        };

        EventsGrid.ItemsSource = FilterEvents(match)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item =>
            {
                var player = item.PlayerId is null ? null : _data.Players.FirstOrDefault(p => p.Id == item.PlayerId);
                var relatedPlayer = item.RelatedPlayerId is null ? null : _data.Players.FirstOrDefault(p => p.Id == item.RelatedPlayerId);
                return new EventRow
                {
                    事件ID = item.Id,
                    状态 = item.IsVoided ? "作废" : "有效",
                    时间 = item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    节次 = item.Period,
                    表钟 = FormatSeconds(item.ClockSecondsRemaining),
                    队伍 = item.Kind == MatchEventKind.ClockControl ? "计时控制" : item.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName,
                    球员 = EventPlayerText(item, player, relatedPlayer),
                    事件 = EventText(item),
                    分值 = item.Points,
                    备注 = item.Note,
                    作废时间 = item.VoidedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                    作废原因 = item.VoidReason,
                    操作人 = item.VoidedBy
                };
            })
            .ToList();
    }

    private IEnumerable<MatchEvent> FilterEvents(Match match)
    {
        var events = _data.Events.Where(item => item.MatchId == match.Id);
        var sideFilter = EventFilterSideCombo.SelectedItem?.ToString() ?? "全部";
        if (sideFilter == "主队")
        {
            events = events.Where(item => item.Side == TeamSide.Home);
        }
        else if (sideFilter == "客队")
        {
            events = events.Where(item => item.Side == TeamSide.Away);
        }

        if (EventFilterKindCombo.SelectedItem is MatchEventKind kind)
        {
            events = events.Where(item => item.Kind == kind);
        }

        var playerFilter = EventPlayerFilterBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(playerFilter))
        {
            events = events.Where(item =>
            {
                var player = item.PlayerId is null ? null : _data.Players.FirstOrDefault(player => player.Id == item.PlayerId);
                return player?.DisplayName.Contains(playerFilter, StringComparison.OrdinalIgnoreCase) == true
                    || player?.Team.Contains(playerFilter, StringComparison.OrdinalIgnoreCase) == true;
            });
        }

        return events;
    }

    private static string FormatSeconds(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static string EventText(MatchEvent item)
    {
        return item.Kind switch
        {
            MatchEventKind.Score => $"得分 +{item.Points}",
            MatchEventKind.Foul => "犯规",
            MatchEventKind.Rebound => "篮板",
            MatchEventKind.Assist => "助攻",
            MatchEventKind.Steal => "抢断",
            MatchEventKind.Block => "盖帽",
            MatchEventKind.Turnover => "失误",
            MatchEventKind.TimeoutRequest => "申请暂停",
            MatchEventKind.ClockControl => string.IsNullOrWhiteSpace(item.Note) ? "计时控制" : item.Note,
            MatchEventKind.RosterAudit => string.IsNullOrWhiteSpace(item.Note) ? "名单审计" : item.Note,
            MatchEventKind.Substitution => "换人",
            _ => item.Kind.ToString()
        };
    }

    private static string EventPlayerText(MatchEvent item, Player? player, Player? relatedPlayer)
    {
        return item.Kind == MatchEventKind.Substitution
            ? $"{player?.DisplayName ?? "未知球员"} -> {relatedPlayer?.DisplayName ?? "未知球员"}"
            : player?.DisplayName ?? EventActorText(item);
    }

    private static string TeamSideText(TeamSide side)
    {
        return side == TeamSide.Home ? "主队" : "客队";
    }

    private static string MatchStatusText(MatchStatus status)
    {
        return status switch
        {
            MatchStatus.NotStarted => "未开始",
            MatchStatus.Running => "进行中",
            MatchStatus.Paused => "暂停中",
            MatchStatus.Interval => "节间",
            MatchStatus.Finished => "已结束",
            _ => status.ToString()
        };
    }

    private void PauseRunningMatchesAfterRestart()
    {
        var runningMatches = _data.Matches
            .Where(match => match.IsClockRunning && match.Status != MatchStatus.Finished)
            .ToList();
        if (runningMatches.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var messages = new List<string>();
        foreach (var match in runningMatches)
        {
            var elapsedSeconds = match.LastClockUpdateUtc is null
                ? 0
                : Math.Max(0, (int)(now - match.LastClockUpdateUtc.Value).TotalSeconds);
            if (elapsedSeconds > 0)
            {
                match.RemainingSeconds = Math.Max(0, match.RemainingSeconds - elapsedSeconds);
            }

            match.IsClockRunning = false;
            match.LastClockUpdateUtc = null;
            match.Status = match.RemainingSeconds == 0
                ? match.CurrentPeriod >= match.PeriodCount ? MatchStatus.Finished : MatchStatus.Interval
                : MatchStatus.Paused;
            if (match.Status == MatchStatus.Finished)
            {
                match.EndedAt ??= now;
            }

            messages.Add($"{match.DisplayName}：扣减 {elapsedSeconds} 秒，恢复为 {MatchStatusText(match.Status)}，剩余 {FormatSeconds(match.RemainingSeconds)}。");
        }

        SaveData();
        MessageBox.Show($"检测到 {runningMatches.Count} 场比赛上次关闭时仍在计时，已按真实经过时间扣减后自动停止计时。\n\n{string.Join("\n", messages)}", "计时已恢复", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SelectPhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            PhotoPathBox.Text = _store.ImportPhoto(dialog.FileName);
            UpdatePhotoPreview();
        }
        catch (IOException ex)
        {
            MessageBox.Show($"无法复制照片: {dialog.FileName}. {ex.Message}", "照片导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SavePlayer_Click(object sender, RoutedEventArgs e)
    {
        var name = PlayerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("球员姓名不能为空。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var player = SelectedPlayer ?? new Player { CreatedAt = DateTime.UtcNow };
        var studentNumber = StudentNumberBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(studentNumber)
            && _data.Players.Any(item => item.Id != player.Id && item.StudentNumber.Equals(studentNumber, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"学号“{studentNumber}”已存在，请检查后再保存。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        player.Name = name;
        player.StudentNumber = studentNumber;
        var selectedTeam = PlayerTeamCombo.SelectedItem as Team;
        if (selectedTeam is null || selectedTeam.Status != "启用")
        {
            MessageBox.Show("普通球员必须选择一个已有且启用的队伍。临时球员或外援请后续通过专门入口登记。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        player.TeamId = selectedTeam.Id;
        player.Team = selectedTeam.Name;
        player.Note = PlayerNoteBox.Text.Trim();
        player.PhotoPath = PhotoPathBox.Text.Trim();
        player.Status = PlayerStatusCombo.SelectedItem?.ToString() ?? "在队";
        player.UpdatedAt = DateTime.UtcNow;

        if (!_data.Players.Any(item => item.Id == player.Id))
        {
            _data.Players.Add(player);
        }

        SaveData();
        RefreshAll();
        PlayersGrid.SelectedItem = null;
        ClearPlayerForm();
    }

    private void NewPlayer_Click(object sender, RoutedEventArgs e)
    {
        PlayersGrid.SelectedItem = null;
        ClearPlayerForm();
        RefreshCustomFields();
    }

    private void DeletePlayer_Click(object sender, RoutedEventArgs e)
    {
        var player = SelectedPlayer;
        if (player is null)
        {
            return;
        }

        if (_data.Events.Any(item => item.PlayerId == player.Id))
        {
            player.Status = "停用";
            player.UpdatedAt = DateTime.UtcNow;
            SaveData();
            RefreshAll();
            MessageBox.Show($"球员“{player.DisplayName}”已有比赛事件，不能物理删除，已改为停用。", "已停用", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show($"确定删除球员“{player.DisplayName}”吗？该球员的自定义字段值和参赛名单也会删除。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _data.Players.Remove(player);
        _data.PlayerFieldValues.RemoveAll(item => item.PlayerId == player.Id);
        _data.Rosters.RemoveAll(item => item.PlayerId == player.Id);
        SaveData();
        ClearPlayerForm();
        RefreshAll();
    }

    private void SaveCustomField_Click(object sender, RoutedEventArgs e)
    {
        var fieldName = CustomFieldNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            MessageBox.Show("自定义字段名不能为空。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var field = _data.PlayerFields.FirstOrDefault(item => item.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            field = new PlayerFieldDefinition
            {
                Name = fieldName,
                DisplayOrder = _data.PlayerFields.Count
            };
            _data.PlayerFields.Add(field);
        }

        var player = SelectedPlayer;
        if (player is not null)
        {
            var value = _data.PlayerFieldValues.FirstOrDefault(item => item.PlayerId == player.Id && item.FieldId == field.Id);
            if (value is null)
            {
                value = new PlayerFieldValue { PlayerId = player.Id, FieldId = field.Id };
                _data.PlayerFieldValues.Add(value);
            }

            value.Value = CustomFieldValueBox.Text.Trim();
        }

        SaveData();
        RefreshCustomFields();
    }

    private void PlayersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var player = SelectedPlayer;
        if (player is null)
        {
            ClearPlayerForm();
            return;
        }

        PlayerNameBox.Text = player.Name;
        StudentNumberBox.Text = player.StudentNumber;
        PlayerTeamCombo.SelectedItem = _data.Teams.FirstOrDefault(team => team.Id == player.TeamId);
        PlayerStatusCombo.SelectedItem = player.Status;
        PlayerNoteBox.Text = player.Note;
        PhotoPathBox.Text = player.PhotoPath;
        RefreshCustomFields();
        UpdatePhotoPreview();
    }

    private void PlayerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPlayers();
    }

    private void ClearPlayerForm()
    {
        PlayerNameBox.Text = "";
        StudentNumberBox.Text = "";
        PlayerTeamCombo.SelectedItem = null;
        PlayerStatusCombo.SelectedItem = "在队";
        PlayerNoteBox.Text = "";
        PhotoPathBox.Text = "";
        CustomFieldValueBox.Text = "";
        UpdatePhotoPreview();
    }

    private void BackupData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var backupPath = _store.Backup();
            MessageBox.Show($"备份完成：{backupPath}", "备份成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"备份失败：{ex.Message}", "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string LoadExportDirectory()
    {
        try
        {
            return _store.LoadExportDirectory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "导出目录读取失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return _workspace.ExportDirectory;
        }
    }

    private void RefreshExportDirectoryText()
    {
        ExportDirectoryText.Text = $"导出目录：{_exportDirectory}";
    }

    private void CreateCompetition_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var workspace = _workspaceManager.CreateCompetition(CompetitionNameBox.Text.Trim(), CompetitionIdBox.Text.Trim());
            SwitchWorkspace(workspace);
            MessageBox.Show($"赛事已创建：{workspace.DirectoryPath}", "赛事管理", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "创建赛事失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportCompetition_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要导入的赛事结构化目录",
            InitialDirectory = Directory.Exists(_workspace.DirectoryPath) ? _workspace.DirectoryPath : AppContext.BaseDirectory
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var workspace = _workspaceManager.ImportWorkspace(dialog.FolderName);
            SwitchWorkspace(workspace);
            MessageBox.Show($"赛事已导入并打开：{workspace.DirectoryPath}", "赛事管理", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "导入赛事失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SwitchCompetition_Click(object sender, RoutedEventArgs e)
    {
        SwitchToSelectedCompetition();
    }

    private void CompetitionListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingCompetitionList)
        {
            return;
        }

        SwitchToSelectedCompetition();
    }

    private void SwitchToSelectedCompetition()
    {
        if (CompetitionListCombo.SelectedItem is not CompetitionWorkspace selected)
        {
            MessageBox.Show("请先从已导入赛事列表中选择赛事。", "赛事管理", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (selected.DirectoryPath.Equals(_workspace.DirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var workspace = _workspaceManager.OpenWorkspace(selected.DirectoryPath);
            SwitchWorkspace(workspace);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "切换赛事失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshCompetitionWorkspace();
        }
    }

    private void SaveCompetition_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateClockFromElapsed();
            SaveData();
            var workspace = _workspaceManager.UpdateManifest(_workspace, CompetitionNameBox.Text.Trim(), CompetitionIdBox.Text.Trim());
            SwitchWorkspace(workspace, saveCurrent: false);
            MessageBox.Show("赛事信息已保存。赛事 ID 修改后，只接受新 ID 归属的比赛日志导入。", "赛事管理", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存赛事失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportCompetition_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择赛事导出目标目录",
            InitialDirectory = Directory.Exists(_exportDirectory) ? _exportDirectory : _workspace.ExportDirectory
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            UpdateClockFromElapsed();
            SaveData();
            var exportPath = _workspaceManager.ExportWorkspace(_workspace, dialog.FolderName);
            MessageBox.Show($"赛事结构化目录已导出：{exportPath}", "赛事导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "赛事导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdatePhotoPreview()
    {
        PhotoPreview.Source = null;
        var relativePath = PhotoPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            PhotoPreviewStatus.Text = "无照片";
            PhotoPreviewStatus.Visibility = Visibility.Visible;
            return;
        }

        var fullPath = _store.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
        {
            PhotoPreviewStatus.Text = "照片文件不存在";
            PhotoPreviewStatus.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(fullPath, UriKind.Absolute);
            image.EndInit();
            PhotoPreview.Source = image;
            PhotoPreviewStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            PhotoPreview.Source = null;
            PhotoPreviewStatus.Text = $"照片无法读取：{ex.Message}";
            PhotoPreviewStatus.Visibility = Visibility.Visible;
        }
    }

    private void SaveTeam_Click(object sender, RoutedEventArgs e)
    {
        var name = TeamNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("队伍名不能为空。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var team = SelectedTeam;
        if (team is null && _data.Teams.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"队伍“{name}”已存在，请从左侧选择该队伍后再编辑。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (team is null)
        {
            team = new Team();
            _data.Teams.Add(team);
        }
        else if (!team.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && _data.Teams.Any(item => item.Id != team.Id && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"队伍“{name}”已存在。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        team.Name = name;
        team.Note = TeamNoteBox.Text.Trim();
        team.Status = "启用";
        foreach (var player in _data.Players.Where(item => item.TeamId == team.Id))
        {
            player.Team = team.Name;
            player.UpdatedAt = DateTime.UtcNow;
        }

        SaveData();
        TeamNameBox.Text = "";
        TeamNoteBox.Text = "";
        RefreshAll();
        TeamsGrid.SelectedItem = null;
    }

    private void DisableTeam_Click(object sender, RoutedEventArgs e)
    {
        var team = SelectedTeam;
        if (team is null)
        {
            return;
        }

        if (team.Status != "启用")
        {
            MessageBox.Show("该队伍已经停用。", "队伍停用", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var playerCount = _data.Players.Count(player => player.TeamId == team.Id);
        var matchCount = _data.Matches.Count(match => match.HomeTeamId == team.Id || match.AwayTeamId == team.Id);
        var usageMessage = playerCount > 0 || matchCount > 0
            ? $"该队伍已被 {playerCount} 名球员、{matchCount} 场比赛使用。停用后不会删除历史数据，但不能再作为新比赛的可选队伍。"
            : "停用后该队伍不会再作为新比赛的可选队伍，历史数据会保留。";
        var result = MessageBox.Show(
            $"{usageMessage}\n\n确认停用队伍“{team.Name}”吗？",
            "确认停用队伍",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        team.Status = "停用";
        SaveData();
        RefreshAll();
    }

    private void TeamsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var team = SelectedTeam;
        if (team is null)
        {
            return;
        }

        TeamNameBox.Text = team.Name;
        TeamNoteBox.Text = team.Note;
    }

    private void CreateMatch_Click(object sender, RoutedEventArgs e)
    {
        var name = MatchNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("比赛名不能为空。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var homeTeam = HomeTeamCombo.SelectedItem as Team;
        var awayTeam = AwayTeamCombo.SelectedItem as Team;
        if (homeTeam is null || awayTeam is null)
        {
            MessageBox.Show("请选择主队和客队。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (homeTeam.Id == awayTeam.Id)
        {
            MessageBox.Show("主队和客队不能相同。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PeriodCountBox.Text.Trim(), out var periodCount) || periodCount <= 0)
        {
            MessageBox.Show("节数必须是正整数。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PeriodLengthBox.Text.Trim(), out var periodLengthMinutes) || periodLengthMinutes <= 0)
        {
            MessageBox.Show("每节分钟必须是正整数。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var periodLength = periodLengthMinutes * 60;

        DateTime? scheduledAt = null;
        var dateText = MatchDateBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(dateText))
        {
            if (!DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                MessageBox.Show("比赛日期格式无法识别。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            scheduledAt = parsedDate;
        }

        var match = new Match
        {
            Name = name,
            HomeTeamId = homeTeam.Id,
            AwayTeamId = awayTeam.Id,
            HomeTeamName = homeTeam.Name,
            AwayTeamName = awayTeam.Name,
            ScheduledAt = scheduledAt,
            Location = MatchLocationBox.Text.Trim(),
            Note = MatchNoteBox.Text.Trim(),
            PeriodCount = periodCount,
            PeriodLengthSeconds = periodLength,
            RemainingSeconds = periodLength,
            Status = MatchStatus.NotStarted
        };

        _data.Matches.Add(match);
        SaveData();
        RefreshMatches();
        MatchCombo.SelectedItem = match;
        RefreshRosters();
    }

    private void AddRoster_Click(object sender, RoutedEventArgs e)
    {
        SaveRosterEntry(null);
    }

    private void UpdateRoster_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRoster;
        if (selected is null)
        {
            MessageBox.Show("请先在名单表中选择要更新的球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var roster = _data.Rosters.FirstOrDefault(item => item.Id == selected.名单ID);
        if (roster is null)
        {
            MessageBox.Show("选中的名单记录已不存在，请刷新后重试。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshRosters();
            return;
        }

        SaveRosterEntry(roster);
    }

    private void RemoveRoster_Click(object sender, RoutedEventArgs e)
    {
        var match = PrepMatch;
        var selected = SelectedRoster;
        if (match is null || selected is null)
        {
            MessageBox.Show("请先选择比赛和要移出的名单记录。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var roster = _data.Rosters.FirstOrDefault(item => item.Id == selected.名单ID);
        if (roster is null)
        {
            MessageBox.Show("选中的名单记录已不存在，请刷新后重试。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshRosters();
            return;
        }

        if (roster.IsOnCourt && match.Status != MatchStatus.NotStarted)
        {
            MessageBox.Show("当前场上球员不能直接移出名单，请在暂停状态先完成换人。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var audit = PrepareRosterAudit(match, $"移出名单：{selected.球员}");
        if (audit is null)
        {
            return;
        }

        var result = MessageBox.Show($"确认将“{selected.球员}”移出本场比赛名单吗？", "确认移出名单", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _data.Rosters.Remove(roster);
        AddRosterAuditEvent(match, roster.Side, audit, $"移出名单：{selected.球员}，原号码 {selected.号码}，原首发 {FormatBool(selected.首发)}");
        SaveData();
        RefreshRelatedViewsAfterPrepChange();
    }

    private void SaveRosterEntry(MatchRoster? existingRoster)
    {
        var match = PrepMatch;
        var player = RosterPlayerCombo.SelectedItem as Player;
        if (match is null || player is null)
        {
            MessageBox.Show("请先选择比赛和球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var side = RosterSideCombo.SelectedItem is TeamSide selectedSide ? selectedSide : TeamSide.Home;
        var actionName = existingRoster is null ? "加入名单" : "更新名单";
        var playerName = player.DisplayName;
        var audit = PrepareRosterAudit(match, $"{actionName}：{TeamSideText(side)} {playerName}");
        if (audit is null)
        {
            return;
        }

        var sideTeamId = side == TeamSide.Home ? match.HomeTeamId : match.AwayTeamId;
        if (player.TeamId is null || sideTeamId is null || player.TeamId != sideTeamId)
        {
            MessageBox.Show("正式比赛名单只能选择所选阵营队伍下的球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var roster = _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id);
        if (roster is not null)
        {
            if (existingRoster is null || roster.Id != existingRoster.Id)
            {
                MessageBox.Show("该球员已在本场比赛名单中。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (existingRoster is not null
            && existingRoster.IsOnCourt
            && match.Status != MatchStatus.NotStarted
            && (existingRoster.PlayerId != player.Id || existingRoster.Side != side))
        {
            MessageBox.Show("已在场球员不能通过名单编辑直接改人或改阵营，请在暂停状态使用换人。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string jerseyNumber;
        try
        {
            jerseyNumber = NormalizeJerseyNumber(JerseyNumberBox.Text.Trim());
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!string.IsNullOrWhiteSpace(jerseyNumber)
            && _data.Rosters.Any(item => item.MatchId == match.Id && item.Side == side && item.Id != existingRoster?.Id && item.JerseyNumber.Equals(jerseyNumber, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"该阵营已存在 {jerseyNumber} 号球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isStarter = StarterBox.IsChecked == true;
        if (match.Status == MatchStatus.NotStarted
            && isStarter
            && _data.Rosters.Count(item => item.MatchId == match.Id && item.Side == side && item.Id != existingRoster?.Id && item.IsStarter) >= 5)
        {
            MessageBox.Show("同一阵营首发/场上球员不能超过 5 人。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var beforeText = existingRoster is null ? "无" : DescribeRoster(existingRoster, match);
        roster = existingRoster ?? new MatchRoster { MatchId = match.Id };
        roster.PlayerId = player.Id;
        roster.Side = side;
        roster.JerseyNumber = jerseyNumber;
        roster.IsStarter = isStarter;
        roster.IsOnCourt = match.Status == MatchStatus.NotStarted
            ? isStarter
            : existingRoster?.IsOnCourt == true && existingRoster.PlayerId == player.Id && existingRoster.Side == side;
        if (existingRoster is null)
        {
            _data.Rosters.Add(roster);
        }

        if (audit.IsRequired)
        {
            AddRosterAuditEvent(match, side, audit, $"{actionName}：{beforeText} -> {DescribeRoster(roster, match)}");
        }

        SaveData();
        RefreshRelatedViewsAfterPrepChange();
    }

    private RosterAudit? PrepareRosterAudit(Match match, string operation)
    {
        if (match.Status == MatchStatus.NotStarted)
        {
            return new RosterAudit(false, "", "");
        }

        var auditOperator = RosterAuditOperatorBox.Text.Trim();
        var auditNote = RosterAuditNoteBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(auditOperator) || string.IsNullOrWhiteSpace(auditNote))
        {
            MessageBox.Show(
                $"当前比赛状态为“{MatchStatusText(match.Status)}”，已进入名单审计修改。\n操作：{operation}\n\n请填写“审计操作人”和“审计备注”后再次确认。",
                "名单审计修改",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        var result = MessageBox.Show(
            $"确认执行名单审计修改？\n状态：{MatchStatusText(match.Status)}\n操作：{operation}\n操作人：{auditOperator}\n备注：{auditNote}\n\n确认后会执行修改，并写入比赛日志。",
            "确认名单审计修改",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes ? new RosterAudit(true, auditOperator, auditNote) : null;
    }

    private void AddRosterAuditEvent(Match match, TeamSide side, RosterAudit audit, string operation)
    {
        if (!audit.IsRequired)
        {
            return;
        }

        _data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = null,
            Side = side,
            Kind = MatchEventKind.RosterAudit,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds,
            Note = $"名单审计修改：{operation}；操作人：{audit.Operator}；备注：{audit.Note}"
        });
        RosterAuditNoteBox.Text = "";
    }

    private string DescribeRoster(MatchRoster roster, Match match)
    {
        var player = _data.Players.FirstOrDefault(item => item.Id == roster.PlayerId);
        return $"{(roster.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName)} {player?.DisplayName ?? "未知球员"}，号码 {roster.JerseyNumber}，首发 {FormatBool(roster.IsStarter)}";
    }

    private MatchRoster? GetRosterForPlayer(Match match, Player player)
    {
        return _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id);
    }

    private static string FormatBool(bool value) => value ? "是" : "否";

    private static string NormalizeJerseyNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0 || number > 99)
        {
            throw new InvalidOperationException("球衣号必须为空，或填写 0 到 99 的整数。");
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }

    private void RostersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = SelectedRoster;
        if (selected is null)
        {
            return;
        }

        var roster = _data.Rosters.FirstOrDefault(item => item.Id == selected.名单ID);
        if (roster is null)
        {
            return;
        }

        RosterSideCombo.SelectedItem = roster.Side;
        RosterPlayerCombo.SelectedItem = _data.Players.FirstOrDefault(player => player.Id == roster.PlayerId);
        JerseyNumberBox.Text = roster.JerseyNumber;
        StarterBox.IsChecked = roster.IsStarter;
    }

    private void MatchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshRosters();
    }

    private void ScoreboardMatchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshScoreboard();
    }

    private void MatchesHistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingMatchHistory)
        {
            return;
        }

        RefreshEvents();
    }

    private void MatchesHistoryGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isRefreshingMatchHistory)
        {
            return;
        }

        RefreshEvents();
    }

    private void RefreshRelatedViewsAfterPrepChange()
    {
        RefreshRosters();
        if (ScoreboardMatch is not null && PrepMatch is not null && ScoreboardMatch.Id == PrepMatch.Id)
        {
            RefreshScoreboard();
        }

        if (LogMatch is not null && PrepMatch is not null && LogMatch.Id == PrepMatch.Id)
        {
            RefreshEvents();
            RefreshMatchHistory();
        }
        else
        {
            RefreshMatchHistory();
        }
    }

    private void RefreshRelatedViewsAfterScoreboardChange()
    {
        RefreshScoreboard();
        if (PrepMatch is not null && ScoreboardMatch is not null && PrepMatch.Id == ScoreboardMatch.Id)
        {
            RefreshRosters();
        }

        if (LogMatch is not null && ScoreboardMatch is not null && LogMatch.Id == ScoreboardMatch.Id)
        {
            RefreshEvents();
            RefreshMatchHistory();
        }
        else
        {
            RefreshMatchHistory();
        }
    }

    private void RefreshRelatedViewsAfterLogChange()
    {
        RefreshEvents();
        RefreshMatchHistory();
        if (ScoreboardMatch is not null && LogMatch is not null && ScoreboardMatch.Id == LogMatch.Id)
        {
            RefreshScoreboard();
        }
    }

    private void OnCourtPlayer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender == HomeOnCourtList && HomeOnCourtList.SelectedItem is Player homePlayer)
        {
            AwayOnCourtList.SelectedItem = null;
            _selectedOnCourtPlayer = homePlayer;
        }
        else if (sender == AwayOnCourtList && AwayOnCourtList.SelectedItem is Player awayPlayer)
        {
            HomeOnCourtList.SelectedItem = null;
            _selectedOnCourtPlayer = awayPlayer;
        }

        RefreshSubstituteCandidates();
        RefreshSelectedOnCourtText();
    }

    private void EventFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        RefreshEvents();
    }

    private void EventFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshEvents();
    }

    private void StartClock_Click(object sender, RoutedEventArgs e)
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            return;
        }

        if (match.Status == MatchStatus.Finished)
        {
            MessageBox.Show("比赛已结束，不能继续计时。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (match.RemainingSeconds <= 0)
        {
            MessageBox.Show("本节时间已经结束，请进入下一节或结束比赛。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ValidateRosterBeforeClockStart(match))
        {
            return;
        }

        match.Status = MatchStatus.Running;
        match.IsClockRunning = true;
        match.LastClockUpdateUtc = DateTime.UtcNow;
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void PauseClock_Click(object sender, RoutedEventArgs e)
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            return;
        }

        if (match.Status != MatchStatus.Running)
        {
            MessageBox.Show("只有进行中的比赛可以暂停。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        UpdateClockFromElapsed();
        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
        if (match.Status == MatchStatus.Running)
        {
            match.Status = MatchStatus.Paused;
        }
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void ResetClock_Click(object sender, RoutedEventArgs e)
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            return;
        }

        if (match.Status == MatchStatus.Finished)
        {
            MessageBox.Show("比赛已结束，不能重置计时。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"确认重置第 {match.CurrentPeriod} 节计时吗？当前本节剩余 {FormatSeconds(match.RemainingSeconds)}，重置后会回到 {FormatSeconds(match.PeriodLengthSeconds)}。",
            "确认重置本节",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        UpdateClockFromElapsed();
        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
        match.RemainingSeconds = match.PeriodLengthSeconds;
        match.Status = match.CurrentPeriod == 1 && !_data.Events.Any(item => item.MatchId == match.Id)
            ? MatchStatus.NotStarted
            : MatchStatus.Paused;
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void NextPeriod_Click(object sender, RoutedEventArgs e)
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            return;
        }

        if (match.Status == MatchStatus.Finished)
        {
            MessageBox.Show("比赛已结束，不能进入下一节。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (match.Status == MatchStatus.Running)
        {
            MessageBox.Show("比赛进行中不能直接进入下一节，请先暂停计时。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateClockFromElapsed();
        if (match.CurrentPeriod >= match.PeriodCount)
        {
            var finish = MessageBox.Show("当前已经是最后一节，是否结束比赛？", "结束比赛", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (finish == MessageBoxResult.Yes)
            {
                FinishMatch(match);
            }

            return;
        }

        if (match.RemainingSeconds > 0)
        {
            var confirm = MessageBox.Show(
                $"当前第 {match.CurrentPeriod} 节仍剩余 {FormatSeconds(match.RemainingSeconds)}。\n确认提前进入下一节吗？该操作会写入比赛日志。",
                "确认进入下一节",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var previousPeriod = match.CurrentPeriod;
        var previousRemaining = match.RemainingSeconds;
        match.CurrentPeriod++;
        match.RemainingSeconds = match.PeriodLengthSeconds;
        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
        match.Status = MatchStatus.Interval;
        AddClockControlEvent(match, $"提前从第 {previousPeriod} 节进入第 {match.CurrentPeriod} 节，原剩余 {FormatSeconds(previousRemaining)}");
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void EndMatch_Click(object sender, RoutedEventArgs e)
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            return;
        }

        if (match.Status == MatchStatus.Finished)
        {
            return;
        }

        var result = MessageBox.Show($"确认结束比赛“{match.DisplayName}”吗？结束后不能继续计时或记录事件。", "确认结束比赛", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        UpdateClockFromElapsed();
        FinishMatch(match);
    }

    private void FinishMatch(Match match)
    {
        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
        match.Status = MatchStatus.Finished;
        match.EndedAt ??= DateTime.UtcNow;
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void AddClockControlEvent(Match match, string note)
    {
        _data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = null,
            Side = TeamSide.Home,
            Kind = MatchEventKind.ClockControl,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds,
            Note = note
        });
    }

    private bool ValidateRosterBeforeClockStart(Match match)
    {
        if (match.Status == MatchStatus.NotStarted)
        {
            foreach (var roster in _data.Rosters.Where(roster => roster.MatchId == match.Id))
            {
                roster.IsOnCourt = roster.IsStarter;
            }
        }

        var homeRosterCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && roster.Side == TeamSide.Home);
        var awayRosterCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && roster.Side == TeamSide.Away);
        if (homeRosterCount > 0 && awayRosterCount > 0)
        {
            var homeOnCourtCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && roster.Side == TeamSide.Home && roster.IsOnCourt);
            var awayOnCourtCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && roster.Side == TeamSide.Away && roster.IsOnCourt);
            if (homeOnCourtCount == 0 || awayOnCourtCount == 0)
            {
                MessageBox.Show(
                    $"开始比赛前双方都至少需要 1 名首发/场上球员。\n当前主队 {homeOnCourtCount} 名，客队 {awayOnCourtCount} 名。",
                    "首发不完整",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (homeOnCourtCount > 5 || awayOnCourtCount > 5)
            {
                MessageBox.Show(
                    $"同一阵营场上球员不能超过 5 人。\n当前主队 {homeOnCourtCount} 名，客队 {awayOnCourtCount} 名。",
                    "场上人数不正确",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            var emptyJerseyCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && string.IsNullOrWhiteSpace(roster.JerseyNumber));
            if (emptyJerseyCount == 0)
            {
                return true;
            }

            var result = MessageBox.Show(
                $"本场名单中仍有 {emptyJerseyCount} 名球员未填写球衣号。确认继续开始比赛吗？",
                "球衣号未完整",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        MessageBox.Show(
            $"开始比赛前主队和客队名单都至少需要 1 名球员。\n当前主队 {homeRosterCount} 名，客队 {awayRosterCount} 名。",
            "名单不完整",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private void UpdateClockFromElapsed()
    {
        var match = ScoreboardMatch;
        if (match?.IsClockRunning != true || match.LastClockUpdateUtc is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var elapsedSeconds = (int)(now - match.LastClockUpdateUtc.Value).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return;
        }

        match.RemainingSeconds = Math.Max(0, match.RemainingSeconds - elapsedSeconds);
        match.LastClockUpdateUtc = match.LastClockUpdateUtc.Value.AddSeconds(elapsedSeconds);
        if (match.RemainingSeconds == 0)
        {
            match.IsClockRunning = false;
            match.LastClockUpdateUtc = null;
            match.Status = match.CurrentPeriod >= match.PeriodCount ? MatchStatus.Finished : MatchStatus.Interval;
            if (match.Status == MatchStatus.Finished)
            {
                match.EndedAt ??= DateTime.UtcNow;
            }
        }

        SaveData();
    }

    private void ScoreOne_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Score, 1);
    private void ScoreTwo_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Score, 2);
    private void ScoreThree_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Score, 3);
    private void Foul_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Foul);
    private void Rebound_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Rebound);
    private void Assist_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Assist);
    private void Steal_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Steal);
    private void Block_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Block);
    private void Turnover_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.Turnover);
    private void TimeoutRequest_Click(object sender, RoutedEventArgs e) => RecordTimeoutRequest();

    private void SubstitutePlayer_Click(object sender, RoutedEventArgs e)
    {
        var match = ScoreboardMatch;
        var outgoingPlayer = ActivePlayer;
        var incomingPlayer = SubstitutePlayerCombo.SelectedItem as Player;
        if (match is null || outgoingPlayer is null || incomingPlayer is null)
        {
            MessageBox.Show("请先选择要换下的场上球员和要换上的候补球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateClockFromElapsed();
        if (match.Status != MatchStatus.Paused)
        {
            MessageBox.Show($"当前比赛状态为“{MatchStatusText(match.Status)}”，只有暂停中才能换人。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var outgoingRoster = GetRosterForPlayer(match, outgoingPlayer);
        var incomingRoster = GetRosterForPlayer(match, incomingPlayer);
        if (outgoingRoster is null || !outgoingRoster.IsOnCourt)
        {
            MessageBox.Show("要换下的球员不在当前场上名单中。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshActivePlayers();
            return;
        }

        if (incomingRoster is null || incomingRoster.Side != outgoingRoster.Side || incomingRoster.IsOnCourt)
        {
            MessageBox.Show("换上球员必须是同一阵营的候补球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshActivePlayers();
            return;
        }

        var result = MessageBox.Show(
            $"确认换人？\n{(outgoingRoster.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName)}：{outgoingPlayer.DisplayName} -> {incomingPlayer.DisplayName}",
            "确认换人",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        outgoingRoster.IsOnCourt = false;
        incomingRoster.IsOnCourt = true;
        _data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = outgoingPlayer.Id,
            RelatedPlayerId = incomingPlayer.Id,
            Side = outgoingRoster.Side,
            Kind = MatchEventKind.Substitution,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds,
            Note = EventNoteBox.Text.Trim()
        });

        _selectedOnCourtPlayer = incomingPlayer;
        EventNoteBox.Text = "";
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void RecordTimeoutRequest()
    {
        var match = ScoreboardMatch;
        if (match is null)
        {
            MessageBox.Show("请先选择比赛。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateClockFromElapsed();
        if (match.Status != MatchStatus.Running)
        {
            MessageBox.Show($"当前比赛状态为“{MatchStatusText(match.Status)}”，只有进行中才能记录暂停。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var player = ActivePlayer;
        var roster = player is null ? null : _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id && item.IsOnCourt);
        if (roster is null)
        {
            MessageBox.Show("请先在场上球员区域选择申请暂停的球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = roster.PlayerId,
            Side = roster.Side,
            Kind = MatchEventKind.TimeoutRequest,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds,
            Note = EventNoteBox.Text.Trim()
        });

        EventNoteBox.Text = "";
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void RecordEvent(MatchEventKind kind, int points = 0)
    {
        var match = ScoreboardMatch;
        var player = ActivePlayer;
        if (match is null || player is null)
        {
            MessageBox.Show("请先选择比赛和记账球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateClockFromElapsed();
        if (match.Status != MatchStatus.Running)
        {
            MessageBox.Show($"当前比赛状态为“{MatchStatusText(match.Status)}”，只有进行中才能记录比赛事件。", "状态不允许", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var roster = _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id && item.IsOnCourt);
        if (roster is null)
        {
            MessageBox.Show("该球员不在当前场上球员名单中。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = player.Id,
            Side = roster.Side,
            Kind = kind,
            Points = points,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds,
            Note = EventNoteBox.Text.Trim()
        });

        EventNoteBox.Text = "";
        SaveData();
        RefreshRelatedViewsAfterScoreboardChange();
    }

    private void UndoLastEvent_Click(object sender, RoutedEventArgs e)
    {
        var match = LogMatch;
        if (match is null)
        {
            return;
        }

        var reason = VoidReasonBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            MessageBox.Show("作废事件必须填写原因，便于赛后复核。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var last = _data.Events
            .Where(item => item.MatchId == match.Id && !item.IsVoided)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        if (last is null)
        {
            return;
        }

        VoidEvent(last, reason);
    }

    private void VoidSelectedEvent_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedEvent;
        if (selected is null)
        {
            MessageBox.Show("请先在比赛日志中选择要作废的事件。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reason = VoidReasonBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            MessageBox.Show("作废事件必须填写原因，便于赛后复核。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var match = LogMatch;
        var item = match is null ? null : _data.Events.FirstOrDefault(item => item.MatchId == match.Id && item.Id == selected.事件ID);
        if (item is null)
        {
            MessageBox.Show("选中的事件已不存在，请刷新后重试。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshEvents();
            return;
        }

        VoidEvent(item, reason);
    }

    private void VoidEvent(MatchEvent item, string reason)
    {
        if (item.IsVoided)
        {
            MessageBox.Show("该事件已经作废。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var player = item.PlayerId is null ? null : _data.Players.FirstOrDefault(player => player.Id == item.PlayerId);
        var actor = player?.DisplayName ?? EventActorText(item);
        var result = MessageBox.Show(
            $"确认作废该事件？\n第 {item.Period} 节 {FormatSeconds(item.ClockSecondsRemaining)}，{TeamSideText(item.Side)}，{actor}，{EventText(item)}。\n\n事件会保留在日志中，并标记为作废。",
            "确认作废事件",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        item.IsVoided = true;
        item.VoidedAt = DateTime.UtcNow;
        item.VoidReason = reason;
        item.VoidedBy = VoidOperatorBox.Text.Trim();
        VoidReasonBox.Text = "";
        SaveData();
        RefreshRelatedViewsAfterLogChange();
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var match = LogMatch;
        if (match is null)
        {
            MessageBox.Show("请先选择要导出的比赛。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var safeName = string.Join("_", match.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "match";
        }

        try
        {
            Directory.CreateDirectory(_exportDirectory);
            var matchDate = (match.ScheduledAt ?? DateTime.Now).ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var exportPath = Path.Combine(_exportDirectory, $"{safeName}-{matchDate}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.csv");
            File.WriteAllText(exportPath, "\uFEFF" + BuildMatchCsv(match), Encoding.UTF8);
            MessageBox.Show($"导出完成：{exportPath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetExportDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 CSV 导出目录",
            InitialDirectory = Directory.Exists(_exportDirectory) ? _exportDirectory : _workspace.ExportDirectory
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _store.SaveExportDirectory(dialog.FolderName);
            _exportDirectory = dialog.FolderName;
            RefreshExportDirectoryText();
            MessageBox.Show($"导出目录已设置为：{_exportDirectory}", "导出目录", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "导出目录设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportSelectedMatchLogs_Click(object sender, RoutedEventArgs e)
    {
        var selectedMatchIds = CheckedMatchHistoryRows.Select(row => row.比赛ID).ToHashSet();
        var matches = selectedMatchIds.Count == 0
            ? LogMatch is null ? new List<Match>() : [LogMatch]
            : _data.Matches.Where(match => selectedMatchIds.Contains(match.Id)).ToList();
        if (matches.Count == 0)
        {
            MessageBox.Show("请先选择要导出的比赛日志。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(_exportDirectory);
            var batchDirectory = Path.Combine(_exportDirectory, "match-logs", $"{_workspace.Manifest.CompetitionId}-{DateTime.Now:yyyyMMdd-HHmmss-fff}");
            Directory.CreateDirectory(batchDirectory);
            var options = new JsonSerializerOptions { WriteIndented = true };
            foreach (var match in matches)
            {
                var export = MatchLogService.Build(_data, _workspace.Manifest.CompetitionId, match);
                var exportPath = Path.Combine(
                    batchDirectory,
                    $"matchlog-{_workspace.Manifest.CompetitionId}-{match.Id}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
                File.WriteAllText(exportPath, JsonSerializer.Serialize(export, options), Encoding.UTF8);
            }

            MessageBox.Show($"已导出 {matches.Count} 个结构化比赛日志：{batchDirectory}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出比赛日志失败：{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportMatchLogs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "比赛日志 JSON|*.json|所有文件|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(_exportDirectory) ? _exportDirectory : _workspace.ExportDirectory
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var imported = 0;
        var skipped = 0;
        var rejected = new List<string>();
        foreach (var fileName in dialog.FileNames)
        {
            try
            {
                var export = JsonSerializer.Deserialize<MatchLogExport>(File.ReadAllText(fileName));
                if (export is null)
                {
                    rejected.Add($"{Path.GetFileName(fileName)}：文件内容为空。");
                    continue;
                }

                var result = MatchLogService.Import(_data, export, _workspace.Manifest.CompetitionId);
                if (result.Status == MatchLogImportStatus.Imported)
                {
                    imported++;
                }
                else if (result.Status == MatchLogImportStatus.Skipped)
                {
                    skipped++;
                }
                else
                {
                    rejected.Add($"{Path.GetFileName(fileName)}：{result.Message}");
                }
            }
            catch (Exception ex)
            {
                rejected.Add($"{Path.GetFileName(fileName)}：{ex.Message}");
            }
        }

        if (imported > 0)
        {
            SaveData();
            RefreshAll();
        }

        var message = $"导入完成。成功 {imported} 个，重复跳过 {skipped} 个，拒绝 {rejected.Count} 个。";
        if (rejected.Count > 0)
        {
            message += "\n\n" + string.Join("\n", rejected.Take(8));
            if (rejected.Count > 8)
            {
                message += $"\n还有 {rejected.Count - 8} 个错误未显示。";
            }
        }

        MessageBox.Show(message, "比赛日志导入", MessageBoxButton.OK, rejected.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private string BuildMatchCsv(Match match)
    {
        var projection = Statistics.Compute(_data, match);
        var builder = new StringBuilder();
        AppendCsvRow(builder, "比赛名称", match.Name);
        AppendCsvRow(builder, "主队", match.HomeTeamName);
        AppendCsvRow(builder, "客队", match.AwayTeamName);
        AppendCsvRow(builder, "日期", match.ScheduledAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "");
        AppendCsvRow(builder, "地点", match.Location);
        AppendCsvRow(builder, "状态", MatchStatusText(match.Status));
        AppendCsvRow(builder, "备注", match.Note);
        builder.AppendLine();

        AppendCsvRow(builder, "球队统计");
        AppendCsvRow(builder, "队伍", "比分", "犯规", "暂停", "篮板", "助攻", "抢断", "盖帽", "失误");
        AppendCsvRow(
            builder,
            match.HomeTeamName,
            projection.HomeScore.ToString(CultureInfo.InvariantCulture),
            projection.HomeFouls.ToString(CultureInfo.InvariantCulture),
            projection.HomeTimeouts.ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Rebounds).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Assists).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Steals).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Blocks).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.HomeTeamName).Sum(item => item.Turnovers).ToString(CultureInfo.InvariantCulture));
        AppendCsvRow(
            builder,
            match.AwayTeamName,
            projection.AwayScore.ToString(CultureInfo.InvariantCulture),
            projection.AwayFouls.ToString(CultureInfo.InvariantCulture),
            projection.AwayTimeouts.ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Rebounds).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Assists).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Steals).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Blocks).ToString(CultureInfo.InvariantCulture),
            projection.PlayerStats.Where(item => item.Side == match.AwayTeamName).Sum(item => item.Turnovers).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();

        AppendCsvRow(builder, "按节团队犯规");
        AppendCsvRow(builder, "节次", match.HomeTeamName, match.AwayTeamName);
        foreach (var item in projection.PeriodTeamStats)
        {
            AppendCsvRow(
                builder,
                item.Period.ToString(CultureInfo.InvariantCulture),
                item.HomeFouls.ToString(CultureInfo.InvariantCulture),
                item.AwayFouls.ToString(CultureInfo.InvariantCulture));
        }
        builder.AppendLine();

        AppendCsvRow(builder, "球员技术统计");
        AppendCsvRow(builder, "队伍", "球员", "得分", "犯规", "篮板", "助攻", "抢断", "盖帽", "失误", "暂停申请");
        foreach (var item in projection.PlayerStats)
        {
            AppendCsvRow(
                builder,
                item.Side,
                item.PlayerName,
                item.Points.ToString(CultureInfo.InvariantCulture),
                item.Fouls.ToString(CultureInfo.InvariantCulture),
                item.Rebounds.ToString(CultureInfo.InvariantCulture),
                item.Assists.ToString(CultureInfo.InvariantCulture),
                item.Steals.ToString(CultureInfo.InvariantCulture),
                item.Blocks.ToString(CultureInfo.InvariantCulture),
                item.Turnovers.ToString(CultureInfo.InvariantCulture),
                item.TimeoutRequests.ToString(CultureInfo.InvariantCulture));
        }
        builder.AppendLine();

        AppendCsvRow(builder, "事件流水");
        AppendCsvRow(builder, "状态", "时间", "节次", "表钟", "队伍", "球员", "事件", "分值", "备注", "作废时间", "作废原因", "操作人");
        foreach (var item in _data.Events.Where(item => item.MatchId == match.Id).OrderBy(item => item.CreatedAt))
        {
            var player = item.PlayerId is null ? null : _data.Players.FirstOrDefault(player => player.Id == item.PlayerId);
            var relatedPlayer = item.RelatedPlayerId is null ? null : _data.Players.FirstOrDefault(player => player.Id == item.RelatedPlayerId);
            AppendCsvRow(
                builder,
                item.IsVoided ? "作废" : "有效",
                item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                item.Period.ToString(CultureInfo.InvariantCulture),
                FormatSeconds(item.ClockSecondsRemaining),
                item.Kind == MatchEventKind.ClockControl ? "计时控制" : item.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName,
                EventPlayerText(item, player, relatedPlayer),
                EventText(item),
                item.Points.ToString(CultureInfo.InvariantCulture),
                item.Note,
                item.VoidedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                item.VoidReason,
                item.VoidedBy);
        }

        return builder.ToString();
    }

    private static void AppendCsvRow(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string EventActorText(MatchEvent item)
    {
        return item.Kind switch
        {
            MatchEventKind.TimeoutRequest => "球队",
            MatchEventKind.RosterAudit => "名单审计",
            MatchEventKind.Substitution => "换人",
            _ => "系统"
        };
    }

    private sealed record RosterAudit(bool IsRequired, string Operator, string Note);

    private sealed class RosterRow
    {
        public Guid 名单ID { get; set; }
        public string 球员 { get; set; } = "";
        public string 队伍 { get; set; } = "";
        public string 号码 { get; set; } = "";
        public bool 首发 { get; set; }
        public bool 在场 { get; set; }
    }

    private sealed class EventRow
    {
        public Guid 事件ID { get; set; }
        public string 状态 { get; set; } = "";
        public string 时间 { get; set; } = "";
        public int 节次 { get; set; }
        public string 表钟 { get; set; } = "";
        public string 队伍 { get; set; } = "";
        public string 球员 { get; set; } = "";
        public string 事件 { get; set; } = "";
        public int 分值 { get; set; }
        public string 备注 { get; set; } = "";
        public string 作废时间 { get; set; } = "";
        public string 作废原因 { get; set; } = "";
        public string 操作人 { get; set; } = "";
    }

    private sealed class MatchHistoryRow
    {
        public bool 导出 { get; set; }
        public Guid 比赛ID { get; set; }
        public string 创建时间 { get; set; } = "";
        public string 比赛名 { get; set; } = "";
        public string 主队 { get; set; } = "";
        public string 客队 { get; set; } = "";
        public string 状态 { get; set; } = "";
        public int 名单数 { get; set; }
        public int 事件数 { get; set; }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        UpdateClockFromElapsed();
        SaveData();
    }
}
