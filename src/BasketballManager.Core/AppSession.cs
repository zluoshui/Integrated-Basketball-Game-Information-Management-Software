namespace BasketballManager;

/// <summary>
/// In-memory session for one open competition workspace.
/// Shared by API host and (later) service extraction from WPF.
/// </summary>
public sealed class AppSession
{
    private readonly object _gate = new();
    private readonly CompetitionWorkspaceManager _workspaceManager;

    public AppSession(CompetitionWorkspaceManager? workspaceManager = null)
    {
        _workspaceManager = workspaceManager ?? new CompetitionWorkspaceManager();
        Workspace = _workspaceManager.OpenOrCreateInitialWorkspace();
        Store = new DataStore(Workspace.DirectoryPath);
        Data = Store.Load();
        ExportDirectory = Store.LoadExportDirectory();
        PauseRunningMatchesAfterRestart();
        Save();
    }

    public CompetitionWorkspaceManager WorkspaceManager => _workspaceManager;
    public CompetitionWorkspace Workspace { get; private set; }
    public DataStore Store { get; private set; }
    public AppData Data { get; private set; }
    public string ExportDirectory { get; private set; }

    public T Read<T>(Func<AppSession, T> action)
    {
        lock (_gate)
        {
            return action(this);
        }
    }

    public void Write(Action<AppSession> action)
    {
        lock (_gate)
        {
            action(this);
        }
    }

    public T Write<T>(Func<AppSession, T> action)
    {
        lock (_gate)
        {
            return action(this);
        }
    }

    public void Save()
    {
        Store.Save(Data);
    }

    public void SwitchWorkspace(CompetitionWorkspace workspace, bool saveCurrent = true)
    {
        if (saveCurrent)
        {
            UpdateAllRunningClocks();
            Save();
        }

        Workspace = workspace;
        Store = new DataStore(Workspace.DirectoryPath);
        Data = Store.Load();
        ExportDirectory = Store.LoadExportDirectory();
        PauseRunningMatchesAfterRestart();
        Save();
        _workspaceManager.SaveCurrentWorkspace(Workspace.DirectoryPath);
    }

    public void Reload()
    {
        Data = Store.Load();
        ExportDirectory = Store.LoadExportDirectory();
    }

    public void SetExportDirectory(string path)
    {
        Store.SaveExportDirectory(path);
        ExportDirectory = path;
    }

    public Match? FindMatch(Guid id) => Data.Matches.FirstOrDefault(match => match.Id == id);

    public void UpdateClockFromElapsed(Match match)
    {
        if (!match.IsClockRunning || match.LastClockUpdateUtc is null)
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
    }

    public void UpdateAllRunningClocks()
    {
        foreach (var match in Data.Matches.Where(item => item.IsClockRunning))
        {
            UpdateClockFromElapsed(match);
        }
    }

    private void PauseRunningMatchesAfterRestart()
    {
        var running = Data.Matches.Where(match => match.IsClockRunning && match.Status != MatchStatus.Finished).ToList();
        if (running.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var match in running)
        {
            var elapsedSeconds = match.LastClockUpdateUtc is null
                ? 0
                : Math.Max(0, (int)(now - match.LastClockUpdateUtc.Value).TotalSeconds);
            match.RemainingSeconds = Math.Max(0, match.RemainingSeconds - elapsedSeconds);
            match.IsClockRunning = false;
            match.LastClockUpdateUtc = null;
            if (match.RemainingSeconds == 0 && match.CurrentPeriod >= match.PeriodCount)
            {
                match.Status = MatchStatus.Finished;
                match.EndedAt ??= now;
            }
            else if (match.Status == MatchStatus.Running)
            {
                match.Status = MatchStatus.Paused;
            }
        }
    }
}
