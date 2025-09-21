using Newtonsoft.Json;

namespace SideSpins.Api.Models;

public class Division
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "division";

    [JsonProperty("league")]
    public string League { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("area")]
    public string Area { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class Team
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "team";

    [JsonProperty("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("captainPlayerId")]
    public string CaptainPlayerId { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class Player
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "player";

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("apaNumber")]
    public string? ApaNumber { get; set; }

    [JsonProperty("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonProperty("authUserId")]
    public string? AuthUserId { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class TeamMembership
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "membership";

    [JsonProperty("teamId")]
    public string TeamId { get; set; } = string.Empty;

    [JsonProperty("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonProperty("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    // Change this property name to match your JSON exactly
    [JsonProperty("skillLevel_9b")]
    public int? SkillLevel_9b { get; set; } // Changed property name to match JSON

    [JsonProperty("joinedAt")]
    public DateTime JoinedAt { get; set; }

    [JsonProperty("leftAt")]
    public DateTime? LeftAt { get; set; }
}

public class LineupPlayer
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonProperty("skillLevel")]
    public int SkillLevel { get; set; }

    [JsonProperty("intendedOrder")]
    public int IntendedOrder { get; set; }

    [JsonProperty("isAlternate")]
    public bool IsAlternate { get; set; }

    [JsonProperty("notes")]
    public string? Notes { get; set; }

    [JsonProperty("availability")]
    public string? Availability { get; set; } // "available", "unavailable", or null
}

public class LineupHistoryEntry
{
    [JsonProperty("at")]
    public DateTime At { get; set; }

    [JsonProperty("by")]
    public string By { get; set; } = string.Empty;

    [JsonProperty("change")]
    public string Change { get; set; } = string.Empty;
}

public class LineupPlan
{
    [JsonProperty("ruleset")]
    public string Ruleset { get; set; } = string.Empty;

    [JsonProperty("maxTeamSkillCap")]
    public int MaxTeamSkillCap { get; set; }

    [JsonProperty("home")]
    public List<LineupPlayer> Home { get; set; } = new();

    [JsonProperty("away")]
    public List<LineupPlayer> Away { get; set; } = new();

    [JsonProperty("totals")]
    public LineupTotals Totals { get; set; } = new();

    [JsonProperty("locked")]
    public bool Locked { get; set; }

    [JsonProperty("lockedBy")]
    public string? LockedBy { get; set; }

    [JsonProperty("lockedAt")]
    public DateTime? LockedAt { get; set; }

    [JsonProperty("history")]
    public List<LineupHistoryEntry> History { get; set; } = new();
}

public class LineupTotals
{
    [JsonProperty("homePlannedSkillSum")]
    public int HomePlannedSkillSum { get; set; }

    [JsonProperty("awayPlannedSkillSum")]
    public int AwayPlannedSkillSum { get; set; }

    [JsonProperty("homeWithinCap")]
    public bool HomeWithinCap { get; set; }

    [JsonProperty("awayWithinCap")]
    public bool AwayWithinCap { get; set; }
}

public class MatchTotals
{
    [JsonProperty("homePoints")]
    public int HomePoints { get; set; }

    [JsonProperty("awayPoints")]
    public int AwayPoints { get; set; }

    [JsonProperty("bonusPoints")]
    public BonusPoints BonusPoints { get; set; } = new();
}

public class BonusPoints
{
    [JsonProperty("home")]
    public int Home { get; set; }

    [JsonProperty("away")]
    public int Away { get; set; }
}

public class TeamMatch
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "teamMatch";

    [JsonProperty("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonProperty("week")]
    public int Week { get; set; }

    [JsonProperty("scheduledAt")]
    public DateTime ScheduledAt { get; set; }

    [JsonProperty("homeTeamId")]
    public string? HomeTeamId { get; set; }

    [JsonProperty("homeTeamName")]
    public string? HomeTeamName { get; set; }

    [JsonProperty("awayTeamId")]
    public string? AwayTeamId { get; set; }

    [JsonProperty("awayTeamName")]
    public string? AwayTeamName { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("lineupPlan")]
    public LineupPlan LineupPlan { get; set; } = new();

    [JsonProperty("playerMatches")]
    public List<object> PlayerMatches { get; set; } = new();

    [JsonProperty("totals")]
    public MatchTotals Totals { get; set; } = new();

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    // New match management fields
    [JsonProperty("teamId")]
    public string? TeamId { get; set; }

    [JsonProperty("playerMatchIds")]
    public List<string> PlayerMatchIds { get; set; } = new();

    [JsonProperty("teamScoreHome")]
    public int TeamScoreHome { get; set; }

    [JsonProperty("teamScoreAway")]
    public int TeamScoreAway { get; set; }

    [JsonProperty("externalLeagueMatchId")]
    public string? ExternalLeagueMatchId { get; set; }

    [JsonProperty("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }

    [JsonProperty("docType")]
    public string DocType { get; set; } = "teamMatch";
}

public class UpdateAvailabilityRequest
{
    [JsonProperty("availability")]
    public string Availability { get; set; } = string.Empty; // "available" or "unavailable"
}

// New match management models
public class PlayerMatch
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("docType")]
    public string DocType { get; set; } = "playerMatch";

    [JsonProperty("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonProperty("teamId")]
    public string TeamId { get; set; } = string.Empty;

    [JsonProperty("teamMatchId")]
    public string TeamMatchId { get; set; } = string.Empty;

    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("homePlayerId")]
    public string HomePlayerId { get; set; } = string.Empty;

    [JsonProperty("awayPlayerId")]
    public string AwayPlayerId { get; set; } = string.Empty;

    [JsonProperty("homePlayerSkill")]
    public int? HomePlayerSkill { get; set; }

    [JsonProperty("awayPlayerSkill")]
    public int? AwayPlayerSkill { get; set; }

    [JsonProperty("gamesWonHome")]
    public int GamesWonHome { get; set; }

    [JsonProperty("gamesWonAway")]
    public int GamesWonAway { get; set; }

    [JsonProperty("pointsHome")]
    public int PointsHome { get; set; }

    [JsonProperty("pointsAway")]
    public int PointsAway { get; set; }

    [JsonProperty("totalRacks")]
    public int TotalRacks { get; set; }

    [JsonProperty("createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [JsonProperty("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}

public class Game
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("docType")]
    public string DocType { get; set; } = "game";

    [JsonProperty("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonProperty("teamId")]
    public string TeamId { get; set; } = string.Empty;

    [JsonProperty("playerMatchId")]
    public string PlayerMatchId { get; set; } = string.Empty;

    [JsonProperty("rackNumber")]
    public int RackNumber { get; set; }

    [JsonProperty("pointsHome")]
    public int PointsHome { get; set; }

    [JsonProperty("pointsAway")]
    public int PointsAway { get; set; }

    [JsonProperty("winner")]
    public string? Winner { get; set; } // "home", "away", or null

    [JsonProperty("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}

// Request/Response models
public record CreateTeamMatchRequest(
    string DivisionId,
    string HomeTeamId,
    string AwayTeamId,
    DateTime MatchDate
);

public record CreatePlayerMatchRequest(
    string DivisionId,
    string HomePlayerId,
    string AwayPlayerId,
    int Order,
    int? HomePlayerSkill = null,
    int? AwayPlayerSkill = null
);

public record UpdatePlayerMatchRequest(
    int? GamesWonHome = null,
    int? GamesWonAway = null,
    int? PointsHome = null,
    int? PointsAway = null
);

public record CreateGameRequest(
    string DivisionId,
    int RackNumber,
    int PointsHome,
    int PointsAway,
    string? Winner = null
);

public record TeamMatchListResponse(List<TeamMatch> Items, string? ContinuationToken);

// Additional request models for MatchService
public record UpdateTeamMatchRequest(
    string DivisionId,
    DateTime? MatchDate = null,
    MatchStatus? Status = null
);

// Enums
public enum MatchStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}

// Summary models for match scoring
public record MatchScoringSummary(
    string TeamMatchId,
    int TotalHomePoints,
    int TotalAwayPoints,
    int TotalGamesWonHome,
    int TotalGamesWonAway,
    IEnumerable<PlayerMatchSummary> PlayerMatches
);

public record PlayerMatchSummary(
    string PlayerMatchId,
    int Order,
    string HomePlayerId,
    string AwayPlayerId,
    int GamesWonHome,
    int GamesWonAway,
    int PointsHome,
    int PointsAway
);
