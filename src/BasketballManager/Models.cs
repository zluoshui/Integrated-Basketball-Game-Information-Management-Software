namespace BasketballManager;

public sealed class AppData
{
    public int SchemaVersion { get; set; } = 1;
    public List<Player> Players { get; set; } = [];
    public List<PlayerFieldDefinition> PlayerFields { get; set; } = [];
    public List<PlayerFieldValue> PlayerFieldValues { get; set; } = [];
    public List<Team> Teams { get; set; } = [];
    public List<Match> Matches { get; set; } = [];
    public List<MatchRoster> Rosters { get; set; } = [];
    public List<MatchEvent> Events { get; set; } = [];
}

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string StudentNumber { get; set; } = "";
    public Guid? TeamId { get; set; }
    public string Team { get; set; } = "";
    public string Note { get; set; } = "";
    public string PhotoPath { get; set; } = "";
    public string Status { get; set; } = "在队";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string DisplayName => string.IsNullOrWhiteSpace(StudentNumber) ? Name : $"{Name} ({StudentNumber})";
}

public sealed class PlayerFieldDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string FieldType { get; set; } = "Text";
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class PlayerFieldValue
{
    public Guid PlayerId { get; set; }
    public Guid FieldId { get; set; }
    public string Value { get; set; } = "";
}

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "启用";
    public string DisplayName => Status == "启用" ? Name : $"{Name} ({Status})";
}

public sealed class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid? HomeTeamId { get; set; }
    public Guid? AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = "主队";
    public string AwayTeamName { get; set; } = "客队";
    public DateTime? ScheduledAt { get; set; }
    public string Location { get; set; } = "";
    public string Note { get; set; } = "";
    public int PeriodCount { get; set; } = 4;
    public int PeriodLengthSeconds { get; set; } = 600;
    public int CurrentPeriod { get; set; } = 1;
    public int RemainingSeconds { get; set; } = 600;
    public MatchStatus Status { get; set; } = MatchStatus.NotStarted;
    public bool IsClockRunning { get; set; }
    public DateTime? LastClockUpdateUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string DisplayName => $"{Name} - {HomeTeamName} vs {AwayTeamName}";
}

public sealed class MatchRoster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public Guid PlayerId { get; set; }
    public TeamSide Side { get; set; }
    public string JerseyNumber { get; set; } = "";
    public bool IsStarter { get; set; }
}

public sealed class MatchEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public Guid? PlayerId { get; set; }
    public TeamSide Side { get; set; }
    public MatchEventKind Kind { get; set; }
    public int Points { get; set; }
    public int Period { get; set; }
    public int ClockSecondsRemaining { get; set; }
    public string Note { get; set; } = "";
    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string VoidReason { get; set; } = "";
    public string VoidedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TeamSide
{
    Home,
    Away
}

public enum MatchStatus
{
    NotStarted,
    Running,
    Paused,
    Interval,
    Finished
}

public enum MatchEventKind
{
    Score,
    Foul,
    Rebound,
    Assist,
    Steal,
    Block,
    Turnover,
    TimeoutRequest,
    ClockControl,
    RosterAudit
}

public sealed class ScoreProjection
{
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }
    public int HomeTimeouts { get; set; }
    public int AwayTimeouts { get; set; }
    public List<PlayerStats> PlayerStats { get; set; } = [];
    public List<PeriodTeamStats> PeriodTeamStats { get; set; } = [];
}

public sealed class PlayerStats
{
    public string PlayerName { get; set; } = "";
    public string Side { get; set; } = "";
    public int Points { get; set; }
    public int Fouls { get; set; }
    public int Rebounds { get; set; }
    public int Assists { get; set; }
    public int Steals { get; set; }
    public int Blocks { get; set; }
    public int Turnovers { get; set; }
    public int TimeoutRequests { get; set; }
}

public sealed class PeriodTeamStats
{
    public int Period { get; set; }
    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }
}
