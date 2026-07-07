using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BasketballManager;

public partial class MainWindow : Window
{
    private readonly DataStore _store = new();
    private readonly DispatcherTimer _timer = new();
    private AppData _data;

    public MainWindow()
    {
        InitializeComponent();

        _data = LoadData();
        PauseRunningMatchesAfterRestart();
        PlayerStatusCombo.ItemsSource = new[] { "在队", "离队", "停用" };
        PlayerStatusCombo.SelectedItem = "在队";
        RosterSideCombo.ItemsSource = Enum.GetValues<TeamSide>();
        RosterSideCombo.SelectedItem = TeamSide.Home;
        ScoreboardSideCombo.ItemsSource = Enum.GetValues<TeamSide>();
        ScoreboardSideCombo.SelectedItem = TeamSide.Home;

        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) =>
        {
            UpdateClockFromElapsed();
            RefreshScoreboard();
        };

        RefreshAll();
        _timer.Start();
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

    private Match? CurrentMatch => MatchCombo.SelectedItem as Match;
    private Player? SelectedPlayer => PlayersGrid.SelectedItem as Player;
    private Team? SelectedTeam => TeamsGrid.SelectedItem as Team;
    private Player? ActivePlayer => ActivePlayerCombo.SelectedItem as Player;

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
        PlayerTeamCombo.ItemsSource = _data.Teams.OrderBy(team => team.Name).ToList();
        RosterPlayerCombo.ItemsSource = _data.Players
            .Where(player => player.Status != "停用")
            .OrderBy(player => player.Name)
            .ToList();
        RefreshCustomFields();
        RefreshActivePlayers();
    }

    private void RefreshTeams()
    {
        var teams = _data.Teams.OrderBy(team => team.Status).ThenBy(team => team.Name).ToList();
        TeamsGrid.ItemsSource = teams;
        PlayerTeamCombo.ItemsSource = teams;
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
        var selectedId = CurrentMatch?.Id;
        MatchCombo.ItemsSource = _data.Matches.OrderByDescending(match => match.CreatedAt).ToList();
        MatchCombo.SelectedItem = _data.Matches.FirstOrDefault(match => match.Id == selectedId) ?? _data.Matches.LastOrDefault();
    }

    private void RefreshRosters()
    {
        var match = CurrentMatch;
        if (match is null)
        {
            RostersGrid.ItemsSource = null;
            ActivePlayerCombo.ItemsSource = null;
            return;
        }

        RostersGrid.ItemsSource = _data.Rosters
            .Where(roster => roster.MatchId == match.Id)
            .Select(roster =>
            {
                var player = _data.Players.FirstOrDefault(item => item.Id == roster.PlayerId);
                return new
                {
                    球员 = player?.DisplayName ?? "未知球员",
                    队伍 = roster.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName,
                    号码 = roster.JerseyNumber,
                    首发 = roster.IsStarter
                };
            })
            .ToList();

        RefreshActivePlayers();
    }

    private void RefreshActivePlayers()
    {
        var match = CurrentMatch;
        ActivePlayerCombo.ItemsSource = match is null
            ? null
            : _data.Rosters
                .Where(roster => roster.MatchId == match.Id)
                .Where(roster => ScoreboardSideCombo.SelectedItem is not TeamSide side || roster.Side == side)
                .Select(roster => _data.Players.FirstOrDefault(player => player.Id == roster.PlayerId))
                .Where(player => player is not null)
                .OrderBy(player => player!.Team)
                .ThenBy(player => player!.Name)
                .ToList();
    }

    private void RefreshScoreboard()
    {
        var match = CurrentMatch;
        if (match is null)
        {
            TimerText.Text = "00:00";
            ScoreText.Text = "0 - 0";
            PeriodText.Text = "未选择比赛";
            StatusText.Text = "无比赛";
            FoulTimeoutText.Text = "0:0 / 0:0";
            StatsGrid.ItemsSource = null;
            RefreshScoreboardControls(null);
            return;
        }

        var projection = Statistics.Compute(_data, match);
        TimerText.Text = FormatSeconds(match.RemainingSeconds);
        ScoreText.Text = $"{projection.HomeScore} - {projection.AwayScore}";
        PeriodText.Text = $"第 {match.CurrentPeriod} / {match.PeriodCount} 节";
        StatusText.Text = MatchStatusText(match.Status);
        FoulTimeoutText.Text = $"{projection.HomeFouls}:{projection.AwayFouls} / {projection.HomeTimeouts}:{projection.AwayTimeouts}";
        StatsGrid.ItemsSource = projection.PlayerStats;
        RefreshScoreboardControls(match);
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
        EventActionsPanel.IsEnabled = isRunning;
    }

    private void RefreshEvents()
    {
        var match = CurrentMatch;
        EventsGrid.ItemsSource = match is null
            ? null
            : _data.Events
                .Where(item => item.MatchId == match.Id)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item =>
                {
                    var player = _data.Players.FirstOrDefault(p => p.Id == item.PlayerId);
                    return new
                    {
                        时间 = item.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        节次 = item.Period,
                        表钟 = FormatSeconds(item.ClockSecondsRemaining),
                        队伍 = item.Side == TeamSide.Home ? match.HomeTeamName : match.AwayTeamName,
                        球员 = player?.DisplayName ?? "未知球员",
                        事件 = EventText(item),
                        分值 = item.Points,
                        备注 = item.Note
                    };
                })
                .ToList();
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
            _ => item.Kind.ToString()
        };
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

        foreach (var match in runningMatches)
        {
            match.IsClockRunning = false;
            match.LastClockUpdateUtc = null;
            match.Status = MatchStatus.Paused;
        }

        SaveData();
        MessageBox.Show($"检测到 {runningMatches.Count} 场比赛上次关闭时仍在计时，已按恢复策略自动暂停。", "计时已恢复", MessageBoxButton.OK, MessageBoxImage.Information);
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
        var teamText = PlayerTeamCombo.Text.Trim();
        var selectedTeam = PlayerTeamCombo.SelectedItem as Team;
        var matchedTeam = selectedTeam?.Name.Equals(teamText, StringComparison.OrdinalIgnoreCase) == true
            ? selectedTeam
            : _data.Teams.FirstOrDefault(team => team.Name.Equals(teamText, StringComparison.OrdinalIgnoreCase));
        player.TeamId = matchedTeam?.Id;
        player.Team = matchedTeam?.Name ?? teamText;
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
        PlayerTeamCombo.Text = player.Team;
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
        PlayerTeamCombo.Text = "";
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

        var team = SelectedTeam ?? _data.Teams.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
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

        foreach (var match in _data.Matches)
        {
            if (match.HomeTeamId == team.Id)
            {
                match.HomeTeamName = team.Name;
            }

            if (match.AwayTeamId == team.Id)
            {
                match.AwayTeamName = team.Name;
            }
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

        if (!int.TryParse(PeriodLengthBox.Text.Trim(), out var periodLength) || periodLength <= 0)
        {
            MessageBox.Show("每节秒数必须是正整数。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
        var match = CurrentMatch;
        var player = RosterPlayerCombo.SelectedItem as Player;
        if (match is null || player is null)
        {
            MessageBox.Show("请先选择比赛和球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var side = RosterSideCombo.SelectedItem is TeamSide selectedSide ? selectedSide : TeamSide.Home;
        var sideTeamId = side == TeamSide.Home ? match.HomeTeamId : match.AwayTeamId;
        if (player.TeamId is not null && sideTeamId is not null && player.TeamId != sideTeamId)
        {
            MessageBox.Show("该球员不属于所选阵营的队伍。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var roster = _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id);
        if (roster is not null)
        {
            MessageBox.Show("该球员已在本场比赛名单中。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var jerseyNumber = JerseyNumberBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(jerseyNumber)
            && _data.Rosters.Any(item => item.MatchId == match.Id && item.Side == side && item.JerseyNumber.Equals(jerseyNumber, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"该阵营已存在 {jerseyNumber} 号球员。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        roster = new MatchRoster { MatchId = match.Id, PlayerId = player.Id };
        roster.Side = side;
        roster.JerseyNumber = jerseyNumber;
        roster.IsStarter = StarterBox.IsChecked == true;
        _data.Rosters.Add(roster);
        SaveData();
        RefreshRosters();
        RefreshScoreboard();
    }

    private void MatchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshRosters();
        RefreshScoreboard();
        RefreshEvents();
    }

    private void ScoreboardSideCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshActivePlayers();
    }

    private void StartClock_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
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
        RefreshScoreboard();
    }

    private void PauseClock_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
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
        RefreshScoreboard();
    }

    private void ResetClock_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
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
        RefreshScoreboard();
    }

    private void NextPeriod_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
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

        match.CurrentPeriod++;
        match.RemainingSeconds = match.PeriodLengthSeconds;
        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
        match.Status = MatchStatus.Interval;
        SaveData();
        RefreshScoreboard();
    }

    private void EndMatch_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
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
        RefreshScoreboard();
    }

    private bool ValidateRosterBeforeClockStart(Match match)
    {
        var homeRosterCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && roster.Side == TeamSide.Home);
        var awayRosterCount = _data.Rosters.Count(roster => roster.MatchId == match.Id && roster.Side == TeamSide.Away);
        if (homeRosterCount > 0 && awayRosterCount > 0)
        {
            return true;
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
        var match = CurrentMatch;
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
        match.LastClockUpdateUtc = now;
        if (match.RemainingSeconds == 0)
        {
            match.IsClockRunning = false;
            match.LastClockUpdateUtc = null;
            match.Status = match.CurrentPeriod >= match.PeriodCount ? MatchStatus.Finished : MatchStatus.Interval;
            if (match.Status == MatchStatus.Finished)
            {
                match.EndedAt ??= DateTime.UtcNow;
            }
            SaveData();
        }
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
    private void TimeoutRequest_Click(object sender, RoutedEventArgs e) => RecordEvent(MatchEventKind.TimeoutRequest);

    private void RecordEvent(MatchEventKind kind, int points = 0)
    {
        var match = CurrentMatch;
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

        var roster = _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id);
        if (roster is null)
        {
            MessageBox.Show("该球员不在当前比赛名单中。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        RefreshScoreboard();
        RefreshEvents();
    }

    private void UndoLastEvent_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
        if (match is null)
        {
            return;
        }

        if (match.Status == MatchStatus.Finished)
        {
            MessageBox.Show("已结束比赛的事件不允许直接撤销。后续更正应通过作废/更正记录保留审计痕迹。", "撤销规则", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var last = _data.Events
            .Where(item => item.MatchId == match.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        if (last is null)
        {
            return;
        }

        var player = _data.Players.FirstOrDefault(item => item.Id == last.PlayerId);
        var result = MessageBox.Show(
            $"确认撤销上一条事件？\n第 {last.Period} 节 {FormatSeconds(last.ClockSecondsRemaining)}，{TeamSideText(last.Side)}，{player?.DisplayName ?? "未知球员"}，{EventText(last)}。",
            "确认撤销事件",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _data.Events.Remove(last);
        SaveData();
        RefreshScoreboard();
        RefreshEvents();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        UpdateClockFromElapsed();
        SaveData();
    }
}
