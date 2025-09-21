using SideSpins.Api.Models;

namespace SideSpins.Api.Services;

public interface IMatchService
{
    // Team Match Operations
    Task<TeamMatch?> GetTeamMatchByIdAsync(string teamMatchId, string divisionId);
    Task<TeamMatch> CreateTeamMatchAsync(CreateTeamMatchRequest request);
    Task<TeamMatch?> UpdateTeamMatchAsync(string teamMatchId, UpdateTeamMatchRequest request);
    Task<bool> DeleteTeamMatchAsync(string teamMatchId, string divisionId);

    // Player Match Operations
    Task<PlayerMatch> CreatePlayerMatchAsync(string teamMatchId, CreatePlayerMatchRequest request);
    Task<PlayerMatch?> GetPlayerMatchByIdAsync(string playerMatchId, string divisionId);
    Task<PlayerMatch?> UpdatePlayerMatchAsync(
        string playerMatchId,
        string divisionId,
        UpdatePlayerMatchRequest request
    );
    Task<IEnumerable<PlayerMatch>> GetPlayerMatchesByTeamMatchIdAsync(
        string teamMatchId,
        string divisionId
    );

    // Game Operations
    Task<Game> RecordGameAsync(string playerMatchId, CreateGameRequest request);
    Task<IEnumerable<Game>> GetGamesByPlayerMatchIdAsync(string playerMatchId, string divisionId);

    // Scoring Operations
    Task<bool> RecomputeMatchScoresAsync(string teamMatchId, string divisionId);
    Task<MatchScoringSummary> GetMatchScoringSummaryAsync(string teamMatchId, string divisionId);

    // Workflow Operations
    Task<bool> CompletePlayerMatchAsync(string playerMatchId, string divisionId);
    Task<bool> FinalizeTeamMatchAsync(string teamMatchId, string divisionId);
}

// Response models for match service operations
public record MatchScoringSummary(
    string TeamMatchId,
    int HomePointsTotal,
    int AwayPointsTotal,
    int HomeGamesWon,
    int AwayGamesWon,
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
