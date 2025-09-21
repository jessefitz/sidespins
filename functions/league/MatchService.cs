using Microsoft.Extensions.Logging;
using SideSpins.Api.Helpers;
using SideSpins.Api.Models;

namespace SideSpins.Api.Services;

public class MatchService : IMatchService
{
    private readonly ILogger<MatchService> _logger;
    private readonly LeagueService _leagueService;
    private readonly IScoreRecomputeService _scoreService;
    private readonly IMatchPersistence _matchPersistence;
    private readonly ITimeProvider _timeProvider;
    private readonly FeatureFlags _featureFlags;

    public MatchService(
        ILogger<MatchService> logger,
        LeagueService leagueService,
        IScoreRecomputeService scoreService,
        IMatchPersistence matchPersistence,
        ITimeProvider timeProvider,
        FeatureFlags featureFlags
    )
    {
        _logger = logger;
        _leagueService = leagueService;
        _scoreService = scoreService;
        _matchPersistence = matchPersistence;
        _timeProvider = timeProvider;
        _featureFlags = featureFlags;
    }

    // ============================================
    // Team Match Operations
    // ============================================

    public async Task<TeamMatch?> GetTeamMatchByIdAsync(string teamMatchId, string divisionId)
    {
        _logger.LogInformation(
            "Getting team match: {TeamMatchId}, Division: {DivisionId}",
            teamMatchId,
            divisionId
        );

        return await _leagueService.GetMatchByIdAsync(teamMatchId, divisionId);
    }

    public async Task<TeamMatch> CreateTeamMatchAsync(CreateTeamMatchRequest request)
    {
        _logger.LogInformation(
            "Creating team match: Division={DivisionId}, Home={HomeTeamId}, Away={AwayTeamId}",
            request.DivisionId,
            request.HomeTeamId,
            request.AwayTeamId
        );

        // Validate teams exist
        var homeTeam = await _leagueService.GetTeamByIdAsync(
            request.HomeTeamId,
            request.DivisionId
        );
        var awayTeam = await _leagueService.GetTeamByIdAsync(
            request.AwayTeamId,
            request.DivisionId
        );

        if (homeTeam == null || awayTeam == null)
        {
            throw new ArgumentException("One or both teams not found");
        }

        // Create team match entity
        var teamMatch = new TeamMatch
        {
            Id = UlidGenerator.NewUlid(),
            DivisionId = request.DivisionId,
            HomeTeamId = request.HomeTeamId,
            AwayTeamId = request.AwayTeamId,
            ScheduledAt = request.MatchDate,
            Status = MatchStatus.Scheduled.ToString(),
            Totals = new MatchTotals { HomePoints = 0, AwayPoints = 0 },
            CreatedAt = _timeProvider.UtcNow,
            UpdatedUtc = _timeProvider.UtcNow,
        };

        var createdMatch = await _leagueService.CreateMatchAsync(teamMatch);

        _logger.LogInformation("Successfully created team match: {TeamMatchId}", createdMatch.Id);
        return createdMatch;
    }

    public async Task<TeamMatch?> UpdateTeamMatchAsync(
        string teamMatchId,
        UpdateTeamMatchRequest request
    )
    {
        _logger.LogInformation("Updating team match: {TeamMatchId}", teamMatchId);

        var existingMatch = await _leagueService.GetMatchByIdAsync(teamMatchId, request.DivisionId);
        if (existingMatch == null)
        {
            return null;
        }

        // Apply updates
        if (request.MatchDate.HasValue)
            existingMatch.ScheduledAt = request.MatchDate.Value;
        if (request.Status.HasValue)
            existingMatch.Status = request.Status.Value.ToString();

        existingMatch.UpdatedUtc = _timeProvider.UtcNow;

        var updatedMatch = await _leagueService.UpdateMatchAsync(
            existingMatch.Id,
            request.DivisionId,
            existingMatch
        );

        _logger.LogInformation("Successfully updated team match: {TeamMatchId}", teamMatchId);
        return updatedMatch;
    }

    public Task<bool> DeleteTeamMatchAsync(string teamMatchId, string divisionId)
    {
        _logger.LogInformation("Deleting team match: {TeamMatchId}", teamMatchId);

        // Note: Delete functionality not yet implemented in persistence layer
        // This is a placeholder for future implementation
        _logger.LogWarning(
            "Delete functionality not yet implemented for team match: {TeamMatchId}",
            teamMatchId
        );

        return Task.FromResult(false);
    }

    // ============================================
    // Player Match Operations
    // ============================================

    public async Task<PlayerMatch> CreatePlayerMatchAsync(
        string teamMatchId,
        CreatePlayerMatchRequest request
    )
    {
        _logger.LogInformation("Creating player match for team match: {TeamMatchId}", teamMatchId);

        // Verify team match exists
        var teamMatch = await GetTeamMatchByIdAsync(teamMatchId, request.DivisionId);
        if (teamMatch == null)
        {
            throw new ArgumentException("Team match not found");
        }

        // Create player match entity
        var playerMatch = new PlayerMatch
        {
            Id = UlidGenerator.NewUlid(),
            TeamMatchId = teamMatchId,
            DivisionId = request.DivisionId,
            HomePlayerId = request.HomePlayerId,
            AwayPlayerId = request.AwayPlayerId,
            Order = request.Order,
            HomePlayerSkill = request.HomePlayerSkill,
            AwayPlayerSkill = request.AwayPlayerSkill,
            GamesWonHome = 0,
            GamesWonAway = 0,
            CreatedUtc = _timeProvider.UtcNow,
            UpdatedUtc = _timeProvider.UtcNow,
        };

        var createdPlayerMatch = await _matchPersistence.CreatePlayerMatchAsync(playerMatch);

        _logger.LogInformation(
            "Successfully created player match: {PlayerMatchId}",
            createdPlayerMatch.Id
        );
        return createdPlayerMatch;
    }

    public async Task<PlayerMatch?> GetPlayerMatchByIdAsync(string playerMatchId, string divisionId)
    {
        _logger.LogInformation("Getting player match: {PlayerMatchId}", playerMatchId);

        return await _matchPersistence.GetPlayerMatchByIdAsync(playerMatchId, divisionId);
    }

    public async Task<PlayerMatch?> UpdatePlayerMatchAsync(
        string playerMatchId,
        string divisionId,
        UpdatePlayerMatchRequest request
    )
    {
        _logger.LogInformation("Updating player match: {PlayerMatchId}", playerMatchId);

        var existingPlayerMatch = await GetPlayerMatchByIdAsync(playerMatchId, divisionId);
        if (existingPlayerMatch == null)
        {
            return null;
        }

        // Apply updates
        if (request.GamesWonHome.HasValue)
            existingPlayerMatch.GamesWonHome = request.GamesWonHome.Value;
        if (request.GamesWonAway.HasValue)
            existingPlayerMatch.GamesWonAway = request.GamesWonAway.Value;

        existingPlayerMatch.UpdatedUtc = _timeProvider.UtcNow;

        var updatedPlayerMatch = await _matchPersistence.UpdatePlayerMatchAsync(
            existingPlayerMatch
        );

        // Recompute team match scores after player match update
        if (_featureFlags.MatchManagementEnabled)
        {
            await RecomputeMatchScoresAsync(existingPlayerMatch.TeamMatchId, divisionId);
        }

        _logger.LogInformation("Successfully updated player match: {PlayerMatchId}", playerMatchId);
        return updatedPlayerMatch;
    }

    public async Task<IEnumerable<PlayerMatch>> GetPlayerMatchesByTeamMatchIdAsync(
        string teamMatchId,
        string divisionId
    )
    {
        _logger.LogInformation("Getting player matches for team match: {TeamMatchId}", teamMatchId);

        return await _matchPersistence.GetPlayerMatchesByTeamMatchIdAsync(teamMatchId, divisionId);
    }

    // ============================================
    // Game Operations
    // ============================================

    public async Task<Game> RecordGameAsync(string playerMatchId, CreateGameRequest request)
    {
        _logger.LogInformation("Recording game for player match: {PlayerMatchId}", playerMatchId);

        // Verify player match exists
        var playerMatch = await GetPlayerMatchByIdAsync(playerMatchId, request.DivisionId);
        if (playerMatch == null)
        {
            throw new ArgumentException("Player match not found");
        }

        // Create game entity
        var game = new Game
        {
            Id = UlidGenerator.NewUlid(),
            PlayerMatchId = playerMatchId,
            DivisionId = request.DivisionId,
            RackNumber = request.RackNumber,
            PointsHome = request.PointsHome,
            PointsAway = request.PointsAway,
            Winner = request.Winner,
            CreatedUtc = _timeProvider.UtcNow,
        };

        var createdGame = await _matchPersistence.CreateGameAsync(game);

        // Recompute scores after game recording
        if (_featureFlags.MatchManagementEnabled)
        {
            await RecomputeMatchScoresAsync(playerMatch.TeamMatchId, request.DivisionId);
        }

        _logger.LogInformation("Successfully recorded game: {GameId}", createdGame.Id);
        return createdGame;
    }

    public async Task<IEnumerable<Game>> GetGamesByPlayerMatchIdAsync(
        string playerMatchId,
        string divisionId
    )
    {
        _logger.LogInformation("Getting games for player match: {PlayerMatchId}", playerMatchId);

        return await _matchPersistence.GetGamesByPlayerMatchIdAsync(playerMatchId, divisionId);
    }

    // ============================================
    // Scoring Operations
    // ============================================

    public async Task<bool> RecomputeMatchScoresAsync(string teamMatchId, string divisionId)
    {
        _logger.LogInformation("Recomputing scores for team match: {TeamMatchId}", teamMatchId);

        try
        {
            // Get team match and player matches
            var teamMatch = await GetTeamMatchByIdAsync(teamMatchId, divisionId);
            if (teamMatch == null)
            {
                _logger.LogWarning(
                    "Team match not found for score recomputation: {TeamMatchId}",
                    teamMatchId
                );
                return false;
            }

            var playerMatches = await GetPlayerMatchesByTeamMatchIdAsync(teamMatchId, divisionId);

            // Recompute each player match
            foreach (var playerMatch in playerMatches)
            {
                var games = await GetGamesByPlayerMatchIdAsync(playerMatch.Id!, divisionId);
                await _scoreService.RecomputePlayerMatchAsync(playerMatch, games);
            }

            // Recompute team match totals
            await _scoreService.RecomputeTeamMatchAsync(teamMatch, playerMatches);

            _logger.LogInformation(
                "Successfully recomputed scores for team match: {TeamMatchId}",
                teamMatchId
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error recomputing scores for team match: {TeamMatchId}",
                teamMatchId
            );
            return false;
        }
    }

    public async Task<MatchScoringSummary> GetMatchScoringSummaryAsync(
        string teamMatchId,
        string divisionId
    )
    {
        _logger.LogInformation(
            "Getting scoring summary for team match: {TeamMatchId}",
            teamMatchId
        );

        var teamMatch = await GetTeamMatchByIdAsync(teamMatchId, divisionId);
        if (teamMatch == null)
        {
            throw new ArgumentException("Team match not found");
        }

        var playerMatches = await GetPlayerMatchesByTeamMatchIdAsync(teamMatchId, divisionId);

        var playerSummaries = new List<PlayerMatchSummary>();
        foreach (var playerMatch in playerMatches)
        {
            var games = await GetGamesByPlayerMatchIdAsync(playerMatch.Id!, divisionId);

            var pointsHome = games.Sum(g => g.PointsHome);
            var pointsAway = games.Sum(g => g.PointsAway);

            playerSummaries.Add(
                new PlayerMatchSummary(
                    playerMatch.Id!,
                    playerMatch.Order,
                    playerMatch.HomePlayerId,
                    playerMatch.AwayPlayerId,
                    playerMatch.GamesWonHome,
                    playerMatch.GamesWonAway,
                    pointsHome,
                    pointsAway
                )
            );
        }

        return new MatchScoringSummary(
            teamMatchId,
            teamMatch.Totals?.HomePoints ?? 0,
            teamMatch.Totals?.AwayPoints ?? 0,
            playerSummaries.Sum(p => p.GamesWonHome),
            playerSummaries.Sum(p => p.GamesWonAway),
            playerSummaries.OrderBy(p => p.Order)
        );
    }

    // ============================================
    // Workflow Operations
    // ============================================

    public async Task<bool> CompletePlayerMatchAsync(string playerMatchId, string divisionId)
    {
        _logger.LogInformation("Completing player match: {PlayerMatchId}", playerMatchId);

        var playerMatch = await GetPlayerMatchByIdAsync(playerMatchId, divisionId);
        if (playerMatch == null)
        {
            return false;
        }

        // Recompute scores to ensure accuracy
        await RecomputeMatchScoresAsync(playerMatch.TeamMatchId, divisionId);

        _logger.LogInformation(
            "Successfully completed player match: {PlayerMatchId}",
            playerMatchId
        );
        return true;
    }

    public async Task<bool> FinalizeTeamMatchAsync(string teamMatchId, string divisionId)
    {
        _logger.LogInformation("Finalizing team match: {TeamMatchId}", teamMatchId);

        var teamMatch = await GetTeamMatchByIdAsync(teamMatchId, divisionId);
        if (teamMatch == null)
        {
            return false;
        }

        // Final score recomputation
        await RecomputeMatchScoresAsync(teamMatchId, divisionId);

        // Update match status to completed
        var updateRequest = new UpdateTeamMatchRequest(divisionId, null, MatchStatus.Completed);

        await UpdateTeamMatchAsync(teamMatchId, updateRequest);

        _logger.LogInformation("Successfully finalized team match: {TeamMatchId}", teamMatchId);
        return true;
    }
}
