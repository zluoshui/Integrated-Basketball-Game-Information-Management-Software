using System.Text.Json.Serialization;

namespace BasketballManager.Api;

public sealed record CompetitionDto(string Id, string Name, string DirectoryPath, bool IsCurrent);
public sealed record TeamDto(Guid Id, string Name, string Note, string Status, int PlayerCount);
public sealed record PlayerFieldDto(Guid Id, string Name, string FieldType, bool IsRequired, int DisplayOrder);
public sealed record PlayerDto(
    Guid Id,
    string Name,
    string StudentNumber,
    Guid? TeamId,
    string Team,
    string Note,
    string PhotoPath,
    string PhotoUrl,
    string Status,
    Dictionary<string, string> CustomFields);

public sealed record MatchSummaryDto(
    Guid Id,
    string Name,
    string HomeTeamName,
    string AwayTeamName,
    Guid? HomeTeamId,
    Guid? AwayTeamId,
    string Status,
    int HomeScore,
    int AwayScore,
    int EventCount,
    int RosterCount,
    int CurrentPeriod,
    int PeriodCount,
    int RemainingSeconds,
    string? ScheduledAt,
    string Location,
    string Note);

public sealed record RosterDto(
    Guid Id,
    Guid MatchId,
    Guid PlayerId,
    string PlayerName,
    string Side,
    string JerseyNumber,
    bool IsStarter,
    bool IsOnCourt);

public sealed record OnCourtPlayerDto(
    Guid PlayerId,
    string Name,
    string JerseyNumber,
    string Side,
    int 得分,
    int 犯规,
    int 篮板,
    int 助攻,
    int 抢断,
    int 盖帽,
    int 失误,
    int 申请暂停);

public sealed record ScoreboardDto(
    Guid MatchId,
    string Name,
    string Status,
    int CurrentPeriod,
    int PeriodCount,
    int RemainingSeconds,
    bool IsClockRunning,
    int HomeScore,
    int AwayScore,
    int HomeFouls,
    int AwayFouls,
    int HomeTimeouts,
    int AwayTimeouts,
    int HomePeriodFouls,
    int AwayPeriodFouls,
    string HomeTeamName,
    string AwayTeamName,
    IReadOnlyList<OnCourtPlayerDto> HomeOnCourt,
    IReadOnlyList<OnCourtPlayerDto> AwayOnCourt);

public sealed record EventDto(
    Guid Id,
    Guid MatchId,
    Guid? PlayerId,
    Guid? RelatedPlayerId,
    string? PlayerName,
    string Side,
    string Kind,
    int Points,
    int Period,
    int ClockSecondsRemaining,
    string Note,
    bool IsVoided,
    string VoidReason,
    string VoidedBy,
    DateTime CreatedAt);

public sealed record CreateCompetitionRequest(string Name, string CompetitionId);
public sealed record UpdateCompetitionRequest(string Name, string CompetitionId);
public sealed record CreateTeamRequest(string Name, string? Note);
public sealed record UpdateTeamRequest(string Name, string? Note, string? Status);
public sealed record CreatePlayerRequest(
    string Name,
    string StudentNumber,
    Guid? TeamId,
    string? Note,
    string? Status,
    Dictionary<string, string>? CustomFields);
public sealed record UpdatePlayerRequest(
    string Name,
    string StudentNumber,
    Guid? TeamId,
    string? Note,
    string? Status,
    Dictionary<string, string>? CustomFields);
public sealed record CreatePlayerFieldRequest(string Name, string FieldType, bool IsRequired);
public sealed record CreateMatchRequest(
    string Name,
    Guid HomeTeamId,
    Guid AwayTeamId,
    string? ScheduledAt,
    string? Location,
    string? Note,
    int PeriodCount,
    int PeriodLengthMinutes);
public sealed record UpsertRosterRequest(
    Guid PlayerId,
    string Side,
    string JerseyNumber,
    bool IsStarter,
    string? AuditOperator,
    string? AuditNote);
public sealed record RecordEventRequest(Guid PlayerId, string Kind, int Points = 0, string? Note = null);
public sealed record SubstitutionRequest(Guid OutgoingPlayerId, Guid IncomingPlayerId, string? Note = null);
public sealed record VoidEventRequest(string Reason, string? Operator);
public sealed record ApiError(string Message);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClockAction
{
    Start,
    Pause,
    Reset,
    NextPeriod,
    End
}
