using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SideSpins.Api.Helpers;
using SideSpins.Api.Models;
using SideSpins.Api.Services;
using SidesSpins.Functions;

namespace SideSpins.Api;

public class MatchesFunctions
{
    private readonly ILogger<MatchesFunctions> _logger;
    private readonly LeagueService _cosmosService;
    private readonly IMembershipService _membershipService;

    public MatchesFunctions(
        ILogger<MatchesFunctions> logger,
        LeagueService cosmosService,
        IMembershipService membershipService
    )
    {
        _logger = logger;
        _cosmosService = cosmosService;
        _membershipService = membershipService;
    }

    [Function("GetMatches")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetMatches(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            var startDate = req.Query["startDate"].FirstOrDefault();
            var endDate = req.Query["endDate"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            IEnumerable<TeamMatch> matches;

            // If date range is provided, use date-filtered query
            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (
                    !DateTime.TryParse(startDate, out var start)
                    || !DateTime.TryParse(endDate, out var end)
                )
                {
                    return new BadRequestObjectResult(
                        "Invalid date format. Use ISO 8601 format (YYYY-MM-DD)"
                    );
                }
                matches = await _cosmosService.GetMatchesByDateRangeAsync(
                    divisionId,
                    start,
                    end.AddDays(1)
                ); // Include end date
            }
            else
            {
                matches = await _cosmosService.GetMatchesByDivisionIdAsync(divisionId);
            }

            return new OkObjectResult(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting matches");
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateMatch")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> CreateMatch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "matches")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var match = JsonConvert.DeserializeObject<TeamMatch>(requestBody);

            if (match == null)
            {
                return new BadRequestObjectResult("Invalid match data");
            }

            // Validate required fields
            if (
                string.IsNullOrEmpty(match.DivisionId)
                || match.Week <= 0
                || match.ScheduledAt == default(DateTime)
            )
            {
                return new BadRequestObjectResult("DivisionId, Week, and ScheduledAt are required");
            }

            var createdMatch = await _cosmosService.CreateMatchAsync(match);
            return new CreatedResult($"/api/matches/{createdMatch.Id}", createdMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating match");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateMatch")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> UpdateMatch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "matches/{matchId}")]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult(
                    "divisionId query parameter is required for match updates"
                );
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var match = JsonConvert.DeserializeObject<TeamMatch>(requestBody);

            if (match == null)
            {
                return new BadRequestObjectResult("Invalid match data");
            }

            var updatedMatch = await _cosmosService.UpdateMatchAsync(matchId, divisionId, match);
            if (updatedMatch == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(updatedMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating match {MatchId}", matchId);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteMatch")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> DeleteMatch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "matches/{matchId}")]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult(
                    "divisionId query parameter is required for match deletion"
                );
            }

            var deleted = await _cosmosService.DeleteMatchAsync(matchId, divisionId);
            if (!deleted)
            {
                return new NotFoundResult();
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting match {MatchId}", matchId);
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateMatchLineup")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> UpdateMatchLineup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "matches/{matchId}/lineup")]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult(
                    "divisionId query parameter is required for match updates"
                );
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var lineupPlan = JsonConvert.DeserializeObject<LineupPlan>(requestBody);

            if (lineupPlan == null)
            {
                return new BadRequestObjectResult("Invalid lineup data");
            }

            // Get existing match to preserve availability information
            var existingMatch = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (existingMatch == null)
            {
                return new NotFoundResult();
            }

            // Preserve availability information for both teams
            var preservedLineupPlan = PreserveAvailabilityInfoForBothTeams(
                existingMatch.LineupPlan,
                lineupPlan
            );

            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(
                matchId,
                divisionId,
                preservedLineupPlan
            );
            if (updatedMatch == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(updatedMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating match lineup {MatchId}", matchId);
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateTeamMatchLineup")]
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> UpdateTeamMatchLineup(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "teams/{teamId}/matches/{matchId}/lineup"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamId,
        string matchId
    )
    {
        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult(
                    "divisionId query parameter is required for match updates"
                );
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var lineupPlan = JsonConvert.DeserializeObject<LineupPlan>(requestBody);

            if (lineupPlan == null)
            {
                return new BadRequestObjectResult("Invalid lineup data");
            }

            // Verify the match exists and that the team is part of it
            var match = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (match == null)
            {
                return new NotFoundResult();
            }

            // Verify the team is either home or away team in this match
            if (match.HomeTeamId != teamId && match.AwayTeamId != teamId)
            {
                return new ForbidResult();
            }

            // Preserve existing availability information when updating lineup
            var preservedLineupPlan = PreserveAvailabilityInfo(
                match.LineupPlan,
                lineupPlan,
                teamId,
                match.HomeTeamId,
                match.AwayTeamId
            );

            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(
                matchId,
                divisionId,
                preservedLineupPlan
            );
            if (updatedMatch == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(updatedMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating team match lineup {TeamId} {MatchId}",
                teamId,
                matchId
            );
            return new StatusCodeResult(500);
        }
    }

    // Public endpoints for the lineup sandbox
    [Function("GetPublicMatches")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetPublicMatches(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/matches")]
            HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var matches = await _cosmosService.GetMatchesByDivisionIdAsync(divisionId);

            // Return simplified match data for public consumption
            var publicMatches = matches.Select(m => new
            {
                id = m.Id,
                date = m.ScheduledAt.ToString("yyyy-MM-dd"),
                scheduledAt = m.ScheduledAt,
                week = m.Week,
                divisionId = m.DivisionId,
                homeTeamId = m.HomeTeamId,
                awayTeamId = m.AwayTeamId,
                status = m.Status,
                // Include existing lineup if available for "start from captain lineup" feature
                hasLineup = m.LineupPlan?.Home?.Any() == true || m.LineupPlan?.Away?.Any() == true,
                lineupPlan = m.LineupPlan,
            });

            return new OkObjectResult(publicMatches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting public matches");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPublicMatchRoster")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetPublicMatchRoster(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "public/matches/{matchId}/roster"
        )]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            // Get the match to determine teams
            var match = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (match == null)
            {
                return new NotFoundResult();
            }

            var roster = new List<object>();

            // Get players for both teams
            if (!string.IsNullOrEmpty(match.HomeTeamId))
            {
                var homeMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(
                    match.HomeTeamId
                );
                var homePlayers = await GetPlayersWithSkills(homeMemberships);
                roster.AddRange(homePlayers);
            }

            if (!string.IsNullOrEmpty(match.AwayTeamId) && match.AwayTeamId != match.HomeTeamId)
            {
                var awayMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(
                    match.AwayTeamId
                );
                var awayPlayers = await GetPlayersWithSkills(awayMemberships);
                roster.AddRange(awayPlayers);
            }

            return new OkObjectResult(roster);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting public match roster for {MatchId}", matchId);
            return new StatusCodeResult(500);
        }
    }

    private async Task<List<object>> GetPlayersWithSkills(IEnumerable<TeamMembership> memberships)
    {
        var playersWithSkills = new List<object>();

        foreach (var membership in memberships.Where(m => m.LeftAt == null)) // Only active players
        {
            var player = await _cosmosService.GetPlayerByIdAsync(membership.PlayerId);
            if (player != null)
            {
                playersWithSkills.Add(
                    new
                    {
                        playerId = player.Id,
                        name = $"{player.FirstName} {player.LastName}".Trim(),
                        skill = membership.SkillLevel_9b ?? 0,
                    }
                );
            }
        }

        return playersWithSkills;
    }

    [Function("DebugCurrentUser")]
    [RequireAuthentication("player")]
    public IActionResult DebugCurrentUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/current-user")]
            HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var userClaims = context.GetUserClaims();
            var userId = context.GetUserId();
            var teamId = context.GetTeamId();
            var teamRole = context.GetTeamRole();

            return new OkObjectResult(
                new
                {
                    userId = userId,
                    teamId = teamId,
                    teamRole = teamRole,
                    userClaims = userClaims,
                    playerId = userClaims?.PlayerId,
                    sub = userClaims?.Sub,
                    sidespinsRole = userClaims?.SidespinsRole,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in debug current user");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdatePlayerAvailability")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> UpdatePlayerAvailability(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "teams/{teamId}/matches/{matchId}/availability"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamId,
        string matchId
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            // Get target player ID - if not specified, defaults to current user
            var targetPlayerId = req.Query["playerId"].FirstOrDefault();

            // Get current user's player ID from JWT claims
            var userClaims = context.GetUserClaims();
            var currentPlayerId = userClaims?.PlayerId;

            _logger.LogInformation(
                "UpdatePlayerAvailability - UserClaims: {@UserClaims}",
                userClaims
            );
            _logger.LogInformation(
                "UpdatePlayerAvailability - PlayerId from claims: {PlayerId}",
                currentPlayerId
            );

            if (string.IsNullOrEmpty(currentPlayerId))
            {
                _logger.LogWarning(
                    "Player ID not found in JWT claims. UserClaims: {@UserClaims}",
                    userClaims
                );
                return new BadRequestObjectResult(
                    new
                    {
                        error = "Player ID not found in authentication token",
                        userClaims = userClaims,
                        debug = "The JWT token does not contain a valid player_id claim",
                    }
                );
            }

            // If no target player specified, use current user's player ID
            if (string.IsNullOrEmpty(targetPlayerId))
            {
                targetPlayerId = currentPlayerId;
            }

            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<UpdateAvailabilityRequest>(requestBody);

            if (request == null || string.IsNullOrEmpty(request.Availability))
            {
                return new BadRequestObjectResult(
                    "Invalid availability data. Must provide 'availability' field with 'available' or 'unavailable' value."
                );
            }

            // Validate availability values
            if (request.Availability != "available" && request.Availability != "unavailable")
            {
                return new BadRequestObjectResult(
                    "Availability must be 'available' or 'unavailable'"
                );
            }

            // Get the match
            var match = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (match == null)
            {
                return new NotFoundResult();
            }

            // Verify the team is part of this match
            if (match.HomeTeamId != teamId && match.AwayTeamId != teamId)
            {
                return new BadRequestObjectResult("Team is not part of this match");
            }

            // Verify the match is today or in the future
            if (match.ScheduledAt.Date < DateTime.UtcNow.Date)
            {
                return new BadRequestObjectResult("Cannot update availability for past matches");
            }

            // Authorization check: Allow if editing own availability OR if user is captain/manager of the team
            bool canEdit = targetPlayerId == currentPlayerId; // Can always edit own availability

            if (!canEdit)
            {
                // Check if current user is captain or manager of this team
                if (userClaims != null && !string.IsNullOrEmpty(userClaims.Sub))
                {
                    var membership = await _membershipService.GetAsync(userClaims.Sub, teamId);
                    if (
                        membership != null
                        && (membership.Role == "captain" || membership.Role == "manager")
                    )
                    {
                        canEdit = true;
                        _logger.LogInformation(
                            "Captain/Manager {CurrentPlayerId} editing availability for player {TargetPlayerId}",
                            currentPlayerId,
                            targetPlayerId
                        );
                    }
                }
            }

            if (!canEdit)
            {
                return new ForbidResult(
                    "You can only edit your own availability unless you are a captain or manager"
                );
            }

            // Determine which lineup to update (home or away)
            var lineup = match.HomeTeamId == teamId ? match.LineupPlan.Home : match.LineupPlan.Away;

            // Find the target player in the lineup
            var playerInLineup = lineup.FirstOrDefault(p => p.PlayerId == targetPlayerId);
            if (playerInLineup == null)
            {
                return new BadRequestObjectResult(
                    $"Player {targetPlayerId} is not in the lineup for this match"
                );
            }

            // Update the player's availability
            playerInLineup.Availability = request.Availability;

            // Update the match in the database
            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(
                matchId,
                divisionId,
                match.LineupPlan
            );
            if (updatedMatch == null)
            {
                return new StatusCodeResult(500);
            }

            return new OkObjectResult(new { success = true, availability = request.Availability });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player availability");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Preserves availability information from existing lineup when updating with new lineup data
    /// </summary>
    private LineupPlan PreserveAvailabilityInfo(
        LineupPlan? existingLineup,
        LineupPlan newLineup,
        string updatingTeamId,
        string? homeTeamId,
        string? awayTeamId
    )
    {
        var preservedLineup = new LineupPlan
        {
            Home = newLineup.Home?.ToList() ?? new List<LineupPlayer>(),
            Away = newLineup.Away?.ToList() ?? new List<LineupPlayer>(),
        };

        // Determine which lineup we're updating
        bool isUpdatingHome = updatingTeamId == homeTeamId;
        var existingTeamLineup = isUpdatingHome ? existingLineup?.Home : existingLineup?.Away;
        var newTeamLineup = isUpdatingHome ? preservedLineup.Home : preservedLineup.Away;

        // Preserve availability info for players that exist in both old and new lineups
        if (existingTeamLineup != null && newTeamLineup != null)
        {
            foreach (var newPlayer in newTeamLineup)
            {
                var existingPlayer = existingTeamLineup.FirstOrDefault(p =>
                    p.PlayerId == newPlayer.PlayerId
                );
                if (existingPlayer != null && !string.IsNullOrEmpty(existingPlayer.Availability))
                {
                    newPlayer.Availability = existingPlayer.Availability;
                }
            }
        }

        // For the team not being updated, preserve the entire lineup (including availability)
        if (isUpdatingHome)
        {
            // We're updating home, so preserve away team lineup completely
            preservedLineup.Away = existingLineup?.Away?.ToList() ?? new List<LineupPlayer>();
        }
        else
        {
            // We're updating away, so preserve home team lineup completely
            preservedLineup.Home = existingLineup?.Home?.ToList() ?? new List<LineupPlayer>();
        }

        return preservedLineup;
    }

    /// <summary>
    /// Preserves availability information for both teams when doing admin lineup updates
    /// </summary>
    private LineupPlan PreserveAvailabilityInfoForBothTeams(
        LineupPlan? existingLineup,
        LineupPlan newLineup
    )
    {
        var preservedLineup = new LineupPlan
        {
            Home = newLineup.Home?.ToList() ?? new List<LineupPlayer>(),
            Away = newLineup.Away?.ToList() ?? new List<LineupPlayer>(),
        };

        // Preserve availability for home team
        if (existingLineup?.Home != null && preservedLineup.Home != null)
        {
            foreach (var newPlayer in preservedLineup.Home)
            {
                var existingPlayer = existingLineup.Home.FirstOrDefault(p =>
                    p.PlayerId == newPlayer.PlayerId
                );
                if (existingPlayer != null && !string.IsNullOrEmpty(existingPlayer.Availability))
                {
                    newPlayer.Availability = existingPlayer.Availability;
                }
            }
        }

        // Preserve availability for away team
        if (existingLineup?.Away != null && preservedLineup.Away != null)
        {
            foreach (var newPlayer in preservedLineup.Away)
            {
                var existingPlayer = existingLineup.Away.FirstOrDefault(p =>
                    p.PlayerId == newPlayer.PlayerId
                );
                if (existingPlayer != null && !string.IsNullOrEmpty(existingPlayer.Availability))
                {
                    newPlayer.Availability = existingPlayer.Availability;
                }
            }
        }

        return preservedLineup;
    }
}
