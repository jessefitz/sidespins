using System.Text.Json.Serialization;

namespace SideSpins.Api.Models;

public class Division
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "division";

    [JsonPropertyName("league")]
    public string League { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("area")]
    public string Area { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class Team
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "team";

    [JsonPropertyName("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("captainPlayerId")]
    public string CaptainPlayerId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class Player
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "player";

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("apaNumber")]
    public string? ApaNumber { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class TeamMembership
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "membership";

    [JsonPropertyName("teamId")]
    public string TeamId { get; set; } = string.Empty;

    [JsonPropertyName("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("skillLevel_9b")]
    public int? SkillLevel9B { get; set; }

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; }

    [JsonPropertyName("leftAt")]
    public DateTime? LeftAt { get; set; }
}

public class LineupPlayer
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("skillLevel")]
    public int SkillLevel { get; set; }

    [JsonPropertyName("intendedOrder")]
    public int IntendedOrder { get; set; }

    [JsonPropertyName("isAlternate")]
    public bool IsAlternate { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class LineupHistoryEntry
{
    [JsonPropertyName("at")]
    public DateTime At { get; set; }

    [JsonPropertyName("by")]
    public string By { get; set; } = string.Empty;

    [JsonPropertyName("change")]
    public string Change { get; set; } = string.Empty;
}

public class LineupPlan
{
    [JsonPropertyName("ruleset")]
    public string Ruleset { get; set; } = string.Empty;

    [JsonPropertyName("maxTeamSkillCap")]
    public int MaxTeamSkillCap { get; set; }

    [JsonPropertyName("home")]
    public List<LineupPlayer> Home { get; set; } = new();

    [JsonPropertyName("away")]
    public List<LineupPlayer> Away { get; set; } = new();

    [JsonPropertyName("totals")]
    public LineupTotals Totals { get; set; } = new();

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    [JsonPropertyName("lockedBy")]
    public string? LockedBy { get; set; }

    [JsonPropertyName("lockedAt")]
    public DateTime? LockedAt { get; set; }

    [JsonPropertyName("history")]
    public List<LineupHistoryEntry> History { get; set; } = new();
}

public class LineupTotals
{
    [JsonPropertyName("homePlannedSkillSum")]
    public int HomePlannedSkillSum { get; set; }

    [JsonPropertyName("awayPlannedSkillSum")]
    public int AwayPlannedSkillSum { get; set; }

    [JsonPropertyName("homeWithinCap")]
    public bool HomeWithinCap { get; set; }

    [JsonPropertyName("awayWithinCap")]
    public bool AwayWithinCap { get; set; }
}

public class MatchTotals
{
    [JsonPropertyName("homePoints")]
    public int HomePoints { get; set; }

    [JsonPropertyName("awayPoints")]
    public int AwayPoints { get; set; }

    [JsonPropertyName("bonusPoints")]
    public BonusPoints BonusPoints { get; set; } = new();
}

public class BonusPoints
{
    [JsonPropertyName("home")]
    public int Home { get; set; }

    [JsonPropertyName("away")]
    public int Away { get; set; }
}

public class TeamMatch
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "teamMatch";

    [JsonPropertyName("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonPropertyName("week")]
    public int Week { get; set; }

    [JsonPropertyName("scheduledAt")]
    public DateTime ScheduledAt { get; set; }

    [JsonPropertyName("homeTeamId")]
    public string? HomeTeamId { get; set; }

    [JsonPropertyName("awayTeamId")]
    public string? AwayTeamId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("lineupPlan")]
    public LineupPlan LineupPlan { get; set; } = new();

    [JsonPropertyName("playerMatches")]
    public List<object> PlayerMatches { get; set; } = new();

    [JsonPropertyName("totals")]
    public MatchTotals Totals { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
