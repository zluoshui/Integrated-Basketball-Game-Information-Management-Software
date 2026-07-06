using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        RosterSideCombo.ItemsSource = Enum.GetValues<TeamSide>();
        RosterSideCombo.SelectedItem = TeamSide.Home;

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
    private Player? ActivePlayer => ActivePlayerCombo.SelectedItem as Player;

    private void RefreshAll()
    {
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
        RosterPlayerCombo.ItemsSource = _data.Players.OrderBy(player => player.Name).ToList();
        RefreshCustomFields();
        RefreshActivePlayers();
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
            FoulTimeoutText.Text = "0:0 / 0:0";
            StatsGrid.ItemsSource = null;
            return;
        }

        var projection = Statistics.Compute(_data, match);
        TimerText.Text = FormatSeconds(match.RemainingSeconds);
        ScoreText.Text = $"{projection.HomeScore} - {projection.AwayScore}";
        PeriodText.Text = $"第 {match.CurrentPeriod} 节";
        FoulTimeoutText.Text = $"{projection.HomeFouls}:{projection.AwayFouls} / {projection.HomeTimeouts}:{projection.AwayTimeouts}";
        StatsGrid.ItemsSource = projection.PlayerStats;
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
                        分值 = item.Points
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
        player.Name = name;
        player.StudentNumber = StudentNumberBox.Text.Trim();
        player.Team = PlayerTeamBox.Text.Trim();
        player.Note = PlayerNoteBox.Text.Trim();
        player.PhotoPath = PhotoPathBox.Text.Trim();
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
        PlayerTeamBox.Text = player.Team;
        PlayerNoteBox.Text = player.Note;
        PhotoPathBox.Text = player.PhotoPath;
        RefreshCustomFields();
    }

    private void PlayerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPlayers();
    }

    private void ClearPlayerForm()
    {
        PlayerNameBox.Text = "";
        StudentNumberBox.Text = "";
        PlayerTeamBox.Text = "";
        PlayerNoteBox.Text = "";
        PhotoPathBox.Text = "";
        CustomFieldValueBox.Text = "";
    }

    private void CreateMatch_Click(object sender, RoutedEventArgs e)
    {
        var name = MatchNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("比赛名不能为空。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PeriodLengthBox.Text.Trim(), out var periodLength) || periodLength <= 0)
        {
            MessageBox.Show("每节秒数必须是正整数。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var match = new Match
        {
            Name = name,
            HomeTeamName = string.IsNullOrWhiteSpace(HomeTeamBox.Text) ? "主队" : HomeTeamBox.Text.Trim(),
            AwayTeamName = string.IsNullOrWhiteSpace(AwayTeamBox.Text) ? "客队" : AwayTeamBox.Text.Trim(),
            PeriodLengthSeconds = periodLength,
            RemainingSeconds = periodLength
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
        var roster = _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id);
        if (roster is null)
        {
            roster = new MatchRoster { MatchId = match.Id, PlayerId = player.Id };
            _data.Rosters.Add(roster);
        }

        roster.Side = side;
        roster.JerseyNumber = JerseyNumberBox.Text.Trim();
        roster.IsStarter = StarterBox.IsChecked == true;
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

    private void StartClock_Click(object sender, RoutedEventArgs e)
    {
        var match = CurrentMatch;
        if (match is null)
        {
            return;
        }

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

        UpdateClockFromElapsed();
        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
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

        match.IsClockRunning = false;
        match.LastClockUpdateUtc = null;
        match.RemainingSeconds = match.PeriodLengthSeconds;
        SaveData();
        RefreshScoreboard();
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

        var roster = _data.Rosters.FirstOrDefault(item => item.MatchId == match.Id && item.PlayerId == player.Id);
        if (roster is null)
        {
            MessageBox.Show("该球员不在当前比赛名单中。", "校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateClockFromElapsed();
        _data.Events.Add(new MatchEvent
        {
            MatchId = match.Id,
            PlayerId = player.Id,
            Side = roster.Side,
            Kind = kind,
            Points = points,
            Period = match.CurrentPeriod,
            ClockSecondsRemaining = match.RemainingSeconds
        });

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

        var last = _data.Events
            .Where(item => item.MatchId == match.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        if (last is null)
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
