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
    private readonly IPlayerService _playerService;
    private readonly IMatchService _matchService;

    public MatchesFunctions(
        ILogger<MatchesFunctions> logger,
        LeagueService cosmosService,
        IMembershipService membershipService,
        IPlayerService playerService,
        IMatchService matchService
    )
    {
        _logger = logger;
        _cosmosService = cosmosService;
        _membershipService = membershipService;
        _playerService = playerService;
        _matchService = matchService;
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
    [AllowApiSecret("Creating matches via administrative API")]
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
    [AllowApiSecret("Updating match details via administrative API")]
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

    [Function("GetTeamMatchDetail")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetTeamMatchDetail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "team-matches/{teamMatchId}")]
            HttpRequest req,
        FunctionContext context,
        string teamMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var divisionId = req.Query["divisionId"].FirstOrDefault();

        _logger.LogInformation(
            "Getting team match detail: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}",
            teamMatchId,
            divisionId
        );

        try
        {
            if (string.IsNullOrEmpty(divisionId))
            {
                _logger.LogWarning(
                    "Missing divisionId parameter for team match {TeamMatchId}",
                    teamMatchId
                );
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var teamMatch = await _matchService.GetTeamMatchByIdAsync(teamMatchId, divisionId);
            if (teamMatch == null)
            {
                _logger.LogWarning(
                    "Team match not found: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}",
                    teamMatchId,
                    divisionId
                );
                return new NotFoundResult();
            }

            _logger.LogInformation(
                "Successfully retrieved team match: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}, "
                    + "HomeTeam={HomeTeamId}, AwayTeam={AwayTeamId}, Status={Status}, ElapsedMs={ElapsedMs}",
                teamMatchId,
                divisionId,
                teamMatch.HomeTeamId,
                teamMatch.AwayTeamId,
                teamMatch.Status,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new OkObjectResult(teamMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting team match {TeamMatchId}, DivisionId={DivisionId}, ElapsedMs={ElapsedMs}",
                teamMatchId,
                divisionId,
                stopwatch.Elapsed.TotalMilliseconds
            );
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

    [Function("VolunteerAsAlternate")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> VolunteerAsAlternate(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "matches/{matchId}/volunteer-alternate"
        )]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            var userId = context.GetUserId();
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            var teamId = req.Query["teamId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            if (string.IsNullOrEmpty(teamId))
            {
                return new BadRequestObjectResult("teamId query parameter is required");
            }

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User ID not found");
            }

            // Get the current user's player record
            var currentUserPlayer = await _playerService.GetPlayerByAuthUserIdAsync(userId);
            if (currentUserPlayer == null)
            {
                return new BadRequestObjectResult("Player not found for current user");
            }

            // Verify the user is authorized to volunteer for this team
            var userMemberships = await _membershipService.GetAllAsync(userId);
            var teamMembership = userMemberships?.FirstOrDefault(m => m.TeamId == teamId);

            if (teamMembership == null)
            {
                return new BadRequestObjectResult("You are not a member of this team");
            }

            // Get the match
            var match = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (match == null)
            {
                return new NotFoundResult();
            }

            // Verify the team is playing in this match
            bool isHomeTeam = match.HomeTeamId == teamId;
            bool isAwayTeam = match.AwayTeamId == teamId;

            if (!isHomeTeam && !isAwayTeam)
            {
                return new BadRequestObjectResult("This team is not playing in this match");
            }

            // Check if the player is already in the lineup (including as alternate)
            var targetLineup = isHomeTeam ? match.LineupPlan.Home : match.LineupPlan.Away;
            var existingPlayerInLineup = targetLineup.FirstOrDefault(p =>
                p.PlayerId == currentUserPlayer.Id
            );

            if (existingPlayerInLineup != null)
            {
                return new BadRequestObjectResult("You are already in the lineup for this match");
            }

            // Get player's skill level - we'll need to look it up from team memberships
            var teamMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(teamId);
            var playerMembership = teamMemberships?.FirstOrDefault(m =>
                m.PlayerId == currentUserPlayer.Id && m.LeftAt == null
            );

            if (playerMembership == null)
            {
                return new BadRequestObjectResult("Player membership not found for this team");
            }

            // Create new alternate lineup player
            var alternatePlayer = new LineupPlayer
            {
                PlayerId = currentUserPlayer.Id,
                SkillLevel = playerMembership.SkillLevel_9b ?? 0,
                IntendedOrder = targetLineup.Count + 1, // Add at the end
                IsAlternate = true,
                Notes = "Volunteered as alternate",
                Availability = "available", // Since they're volunteering, they're available
            };

            // Add the alternate to the appropriate team's lineup
            targetLineup.Add(alternatePlayer);

            // Update the match with the new lineup
            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(
                matchId,
                divisionId,
                match.LineupPlan
            );

            if (updatedMatch == null)
            {
                return new StatusCodeResult(500);
            }

            return new OkObjectResult(
                new { message = "Successfully volunteered as alternate", match = updatedMatch }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error volunteering as alternate for match {MatchId}", matchId);
            return new StatusCodeResult(500);
        }
    }

    [Function("VolunteerPlayerAsAlternate")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> VolunteerPlayerAsAlternate(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "matches/{matchId}/volunteer-player-alternate"
        )]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            var userId = context.GetUserId();
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            var teamId = req.Query["teamId"].FirstOrDefault();
            var playerId = req.Query["playerId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            if (string.IsNullOrEmpty(teamId))
            {
                return new BadRequestObjectResult("teamId query parameter is required");
            }

            if (string.IsNullOrEmpty(playerId))
            {
                return new BadRequestObjectResult("playerId query parameter is required");
            }

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User ID not found");
            }

            // Get the current user's player record to verify they are captain/manager
            var currentUserPlayer = await _playerService.GetPlayerByAuthUserIdAsync(userId);
            if (currentUserPlayer == null)
            {
                return new BadRequestObjectResult("Player not found for current user");
            }

            // Verify the current user is captain or manager of this team
            var teamMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(teamId);
            var currentUserMembership = teamMemberships?.FirstOrDefault(m =>
                m.PlayerId == currentUserPlayer.Id && m.LeftAt == null
            );

            if (
                currentUserMembership == null
                || (
                    currentUserMembership.Role != "captain"
                    && currentUserMembership.Role != "manager"
                )
            )
            {
                return new ForbidResult(
                    "Only team captains and managers can volunteer players as alternates"
                );
            }

            // Verify the target player is a member of this team
            var targetPlayerMembership = teamMemberships?.FirstOrDefault(m =>
                m.PlayerId == playerId && m.LeftAt == null
            );

            if (targetPlayerMembership == null)
            {
                return new BadRequestObjectResult("Target player is not a member of this team");
            }

            // Get the match
            var match = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (match == null)
            {
                return new NotFoundResult();
            }

            // Verify the team is playing in this match
            bool isHomeTeam = match.HomeTeamId == teamId;
            bool isAwayTeam = match.AwayTeamId == teamId;

            if (!isHomeTeam && !isAwayTeam)
            {
                return new BadRequestObjectResult("This team is not playing in this match");
            }

            // Check if the player is already in the lineup (including as alternate)
            var targetLineup = isHomeTeam ? match.LineupPlan.Home : match.LineupPlan.Away;
            var existingPlayerInLineup = targetLineup.FirstOrDefault(p => p.PlayerId == playerId);

            if (existingPlayerInLineup != null)
            {
                return new BadRequestObjectResult("Player is already in the lineup for this match");
            }

            // Create new alternate lineup player
            var alternatePlayer = new LineupPlayer
            {
                PlayerId = playerId,
                SkillLevel = targetPlayerMembership.SkillLevel_9b ?? 0,
                IntendedOrder = targetLineup.Count + 1, // Add at the end
                IsAlternate = true,
                Notes =
                    $"Volunteered as alternate by {currentUserPlayer.FirstName} {currentUserPlayer.LastName}",
                Availability = null, // Leave as null since captain is volunteering them
            };

            // Add the alternate to the appropriate team's lineup
            targetLineup.Add(alternatePlayer);

            // Update the match with the new lineup
            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(
                matchId,
                divisionId,
                match.LineupPlan
            );

            if (updatedMatch == null)
            {
                return new StatusCodeResult(500);
            }

            return new OkObjectResult(
                new
                {
                    message = "Successfully volunteered player as alternate",
                    match = updatedMatch,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error volunteering player as alternate for match {MatchId}",
                matchId
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("RemovePlayerAsAlternate")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> RemovePlayerAsAlternate(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "matches/{matchId}/remove-alternate"
        )]
            HttpRequest req,
        FunctionContext context,
        string matchId
    )
    {
        try
        {
            var userId = context.GetUserId();
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            var teamId = req.Query["teamId"].FirstOrDefault();
            var playerId = req.Query["playerId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            if (string.IsNullOrEmpty(teamId))
            {
                return new BadRequestObjectResult("teamId query parameter is required");
            }

            if (string.IsNullOrEmpty(playerId))
            {
                return new BadRequestObjectResult("playerId query parameter is required");
            }

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User ID not found");
            }

            // Get the current user's player record to verify they are captain/manager
            var currentUserPlayer = await _playerService.GetPlayerByAuthUserIdAsync(userId);
            if (currentUserPlayer == null)
            {
                return new BadRequestObjectResult("Player not found for current user");
            }

            // Verify the current user is captain or manager of this team
            var teamMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(teamId);
            var currentUserMembership = teamMemberships?.FirstOrDefault(m =>
                m.PlayerId == currentUserPlayer.Id && m.LeftAt == null
            );

            if (
                currentUserMembership == null
                || (
                    currentUserMembership.Role != "captain"
                    && currentUserMembership.Role != "manager"
                )
            )
            {
                return new ForbidResult(
                    "Only team captains and managers can remove player alternates"
                );
            }

            // Get the match
            var match = await _cosmosService.GetMatchByIdAsync(matchId, divisionId);
            if (match == null)
            {
                return new NotFoundResult();
            }

            // Verify the team is playing in this match
            bool isHomeTeam = match.HomeTeamId == teamId;
            bool isAwayTeam = match.AwayTeamId == teamId;

            if (!isHomeTeam && !isAwayTeam)
            {
                return new BadRequestObjectResult("This team is not playing in this match");
            }

            // Find and remove the alternate player from the lineup
            var targetLineup = isHomeTeam ? match.LineupPlan.Home : match.LineupPlan.Away;
            var alternatePlayer = targetLineup.FirstOrDefault(p =>
                p.PlayerId == playerId && p.IsAlternate
            );

            if (alternatePlayer == null)
            {
                return new BadRequestObjectResult("Player is not an alternate in this match");
            }

            // Remove the alternate from the lineup
            targetLineup.Remove(alternatePlayer);

            // Update the match with the new lineup
            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(
                matchId,
                divisionId,
                match.LineupPlan
            );

            if (updatedMatch == null)
            {
                return new StatusCodeResult(500);
            }

            return new OkObjectResult(
                new { message = "Successfully removed player as alternate", match = updatedMatch }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing player as alternate for match {MatchId}", matchId);
            return new StatusCodeResult(500);
        }
    }

    // ============================================
    // PlayerMatch Management Endpoints
    // ============================================

    [Function("CreatePlayerMatch")]
    [RequireAuthentication("admin")]
    [AllowApiSecret("Creating player matches via administrative API")]
    public async Task<IActionResult> CreatePlayerMatch(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "team-matches/{teamMatchId}/player-matches"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "Creating player match for team match: TeamMatchId={TeamMatchId}",
            teamMatchId
        );

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var createRequest = JsonConvert.DeserializeObject<CreatePlayerMatchRequest>(
                requestBody
            );

            if (createRequest == null)
            {
                _logger.LogWarning(
                    "Invalid player match data for team match {TeamMatchId}",
                    teamMatchId
                );
                return new BadRequestObjectResult("Invalid player match data");
            }

            _logger.LogInformation(
                "Player match creation request: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}, "
                    + "HomePlayer={HomePlayerId}, AwayPlayer={AwayPlayerId}, Order={Order}",
                teamMatchId,
                createRequest.DivisionId,
                createRequest.HomePlayerId,
                createRequest.AwayPlayerId,
                createRequest.Order
            );

            // Validate required fields
            if (
                string.IsNullOrEmpty(createRequest.DivisionId)
                || string.IsNullOrEmpty(createRequest.HomePlayerId)
                || string.IsNullOrEmpty(createRequest.AwayPlayerId)
                || createRequest.Order <= 0
            )
            {
                _logger.LogWarning(
                    "Missing required fields for player match creation: TeamMatchId={TeamMatchId}, "
                        + "DivisionId={DivisionId}, HomePlayer={HomePlayerId}, AwayPlayer={AwayPlayerId}, Order={Order}",
                    teamMatchId,
                    createRequest.DivisionId,
                    createRequest.HomePlayerId,
                    createRequest.AwayPlayerId,
                    createRequest.Order
                );
                return new BadRequestObjectResult(
                    "DivisionId, HomePlayerId, AwayPlayerId, and Order (> 0) are required"
                );
            }

            // Verify team match exists
            var teamMatch = await _cosmosService.GetMatchByIdAsync(
                teamMatchId,
                createRequest.DivisionId
            );
            if (teamMatch == null)
            {
                _logger.LogWarning(
                    "Team match not found for player match creation: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}",
                    teamMatchId,
                    createRequest.DivisionId
                );
                return new NotFoundObjectResult("Team match not found");
            }

            // Create PlayerMatch entity
            var createdPlayerMatch = await _matchService.CreatePlayerMatchAsync(
                teamMatchId,
                createRequest
            );

            _logger.LogInformation(
                "Successfully created player match: PlayerMatchId={PlayerMatchId}, TeamMatchId={TeamMatchId}, "
                    + "DivisionId={DivisionId}, HomePlayer={HomePlayerId}, AwayPlayer={AwayPlayerId}, "
                    + "Order={Order}, ElapsedMs={ElapsedMs}",
                createdPlayerMatch.Id,
                teamMatchId,
                createRequest.DivisionId,
                createRequest.HomePlayerId,
                createRequest.AwayPlayerId,
                createRequest.Order,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new CreatedResult(
                $"/api/team-matches/{teamMatchId}/player-matches/{createdPlayerMatch.Id}",
                createdPlayerMatch
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating player match for team match {TeamMatchId}, ElapsedMs={ElapsedMs}",
                teamMatchId,
                stopwatch.Elapsed.TotalMilliseconds
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPlayerMatch")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetPlayerMatch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "player-matches/{playerMatchId}")]
            HttpRequest req,
        FunctionContext context,
        string playerMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var divisionId = req.Query["divisionId"].FirstOrDefault();

        _logger.LogInformation(
            "Getting player match: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}",
            playerMatchId,
            divisionId
        );

        try
        {
            if (string.IsNullOrEmpty(divisionId))
            {
                _logger.LogWarning(
                    "Missing divisionId parameter for player match {PlayerMatchId}",
                    playerMatchId
                );
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var playerMatch = await _matchService.GetPlayerMatchByIdAsync(
                playerMatchId,
                divisionId
            );
            if (playerMatch == null)
            {
                _logger.LogWarning(
                    "Player match not found: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}",
                    playerMatchId,
                    divisionId
                );
                return new NotFoundResult();
            }

            _logger.LogInformation(
                "Successfully retrieved player match: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}, "
                    + "TeamMatchId={TeamMatchId}, Order={Order}, GamesWonHome={GamesWonHome}, "
                    + "GamesWonAway={GamesWonAway}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                divisionId,
                playerMatch.TeamMatchId,
                playerMatch.Order,
                playerMatch.GamesWonHome,
                playerMatch.GamesWonAway,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new OkObjectResult(playerMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting player match {PlayerMatchId}, DivisionId={DivisionId}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                divisionId,
                stopwatch.Elapsed.TotalMilliseconds
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdatePlayerMatch")]
    [RequireAuthentication("admin")]
    [AllowApiSecret("Updating player match details via administrative API")]
    public async Task<IActionResult> UpdatePlayerMatch(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "player-matches/{playerMatchId}"
        )]
            HttpRequest req,
        FunctionContext context,
        string playerMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var divisionId = req.Query["divisionId"].FirstOrDefault();

        _logger.LogInformation(
            "Updating player match: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}",
            playerMatchId,
            divisionId
        );

        try
        {
            if (string.IsNullOrEmpty(divisionId))
            {
                _logger.LogWarning(
                    "Missing divisionId parameter for player match update: PlayerMatchId={PlayerMatchId}",
                    playerMatchId
                );
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updateRequest = JsonConvert.DeserializeObject<UpdatePlayerMatchRequest>(
                requestBody
            );

            if (updateRequest == null)
            {
                _logger.LogWarning(
                    "Invalid update data for player match {PlayerMatchId}",
                    playerMatchId
                );
                return new BadRequestObjectResult("Invalid update data");
            }

            _logger.LogInformation(
                "Player match update request: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}, "
                    + "GamesWonHome={GamesWonHome}, GamesWonAway={GamesWonAway}",
                playerMatchId,
                divisionId,
                updateRequest.GamesWonHome,
                updateRequest.GamesWonAway
            );

            // Update player match using MatchService
            var updatedPlayerMatch = await _matchService.UpdatePlayerMatchAsync(
                playerMatchId,
                divisionId,
                updateRequest
            );
            if (updatedPlayerMatch == null)
            {
                _logger.LogWarning(
                    "Player match not found for update: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}",
                    playerMatchId,
                    divisionId
                );
                return new NotFoundResult();
            }

            _logger.LogInformation(
                "Successfully updated player match: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}, "
                    + "GamesWonHome={GamesWonHome}, GamesWonAway={GamesWonAway}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                divisionId,
                updatedPlayerMatch.GamesWonHome,
                updatedPlayerMatch.GamesWonAway,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new OkObjectResult(updatedPlayerMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating player match {PlayerMatchId}, DivisionId={DivisionId}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                divisionId,
                stopwatch.Elapsed.TotalMilliseconds
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPlayerMatchesByTeamMatch")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetPlayerMatchesByTeamMatch(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "team-matches/{teamMatchId}/player-matches"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var divisionId = req.Query["divisionId"].FirstOrDefault();

        _logger.LogInformation(
            "Getting player matches for team match: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}",
            teamMatchId,
            divisionId
        );

        try
        {
            if (string.IsNullOrEmpty(divisionId))
            {
                _logger.LogWarning(
                    "Missing divisionId parameter for team match player matches: TeamMatchId={TeamMatchId}",
                    teamMatchId
                );
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var playerMatches = await _matchService.GetPlayerMatchesByTeamMatchIdAsync(
                teamMatchId,
                divisionId
            );

            _logger.LogInformation(
                "Successfully retrieved player matches: TeamMatchId={TeamMatchId}, DivisionId={DivisionId}, "
                    + "Count={Count}, ElapsedMs={ElapsedMs}",
                teamMatchId,
                divisionId,
                playerMatches?.Count() ?? 0,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new OkObjectResult(playerMatches);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting player matches for team match {TeamMatchId}, DivisionId={DivisionId}, ElapsedMs={ElapsedMs}",
                teamMatchId,
                divisionId,
                stopwatch.Elapsed.TotalMilliseconds
            );
            return new StatusCodeResult(500);
        }
    }

    // ============================================
    // Game Recording Endpoints
    // ============================================

    [Function("RecordGame")]
    [RequireAuthentication("admin")]
    [AllowApiSecret("Recording game results via administrative API")]
    public async Task<IActionResult> RecordGame(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "player-matches/{playerMatchId}/games"
        )]
            HttpRequest req,
        FunctionContext context,
        string playerMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation(
            "Recording game result for player match: PlayerMatchId={PlayerMatchId}",
            playerMatchId
        );

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var createRequest = JsonConvert.DeserializeObject<CreateGameRequest>(requestBody);

            if (createRequest == null)
            {
                _logger.LogWarning(
                    "Invalid game data for player match {PlayerMatchId}",
                    playerMatchId
                );
                return new BadRequestObjectResult("Invalid game data");
            }

            _logger.LogInformation(
                "Game recording request: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}, "
                    + "RackNumber={RackNumber}, Winner={Winner}, PointsHome={PointsHome}, PointsAway={PointsAway}",
                playerMatchId,
                createRequest.DivisionId,
                createRequest.RackNumber,
                createRequest.Winner,
                createRequest.PointsHome,
                createRequest.PointsAway
            );

            // Validate required fields
            if (
                string.IsNullOrEmpty(createRequest.DivisionId)
                || createRequest.RackNumber <= 0
                || string.IsNullOrEmpty(createRequest.Winner)
            )
            {
                _logger.LogWarning(
                    "Missing required fields for game recording: PlayerMatchId={PlayerMatchId}, "
                        + "DivisionId={DivisionId}, RackNumber={RackNumber}, Winner={Winner}",
                    playerMatchId,
                    createRequest.DivisionId,
                    createRequest.RackNumber,
                    createRequest.Winner
                );
                return new BadRequestObjectResult(
                    "DivisionId, RackNumber (> 0), and Winner are required"
                );
            }

            // Record game using MatchService
            var createdGame = await _matchService.RecordGameAsync(playerMatchId, createRequest);

            _logger.LogInformation(
                "Successfully recorded game: GameId={GameId}, PlayerMatchId={PlayerMatchId}, "
                    + "DivisionId={DivisionId}, RackNumber={RackNumber}, Winner={Winner}, "
                    + "PointsHome={PointsHome}, PointsAway={PointsAway}, ElapsedMs={ElapsedMs}",
                createdGame.Id,
                playerMatchId,
                createRequest.DivisionId,
                createRequest.RackNumber,
                createRequest.Winner,
                createRequest.PointsHome,
                createRequest.PointsAway,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new CreatedResult(
                $"/api/player-matches/{playerMatchId}/games/{createdGame.Id}",
                createdGame
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error recording game for player match {PlayerMatchId}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                stopwatch.Elapsed.TotalMilliseconds
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("GetGamesByPlayerMatch")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetGamesByPlayerMatch(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "player-matches/{playerMatchId}/games"
        )]
            HttpRequest req,
        FunctionContext context,
        string playerMatchId
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var divisionId = req.Query["divisionId"].FirstOrDefault();

        _logger.LogInformation(
            "Getting games for player match: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}",
            playerMatchId,
            divisionId
        );

        try
        {
            if (string.IsNullOrEmpty(divisionId))
            {
                _logger.LogWarning(
                    "Missing divisionId parameter for player match games: PlayerMatchId={PlayerMatchId}",
                    playerMatchId
                );
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var games = await _matchService.GetGamesByPlayerMatchIdAsync(playerMatchId, divisionId);

            _logger.LogInformation(
                "Successfully retrieved games: PlayerMatchId={PlayerMatchId}, DivisionId={DivisionId}, "
                    + "Count={Count}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                divisionId,
                games?.Count() ?? 0,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return new OkObjectResult(games);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting games for player match {PlayerMatchId}, DivisionId={DivisionId}, ElapsedMs={ElapsedMs}",
                playerMatchId,
                divisionId,
                stopwatch.Elapsed.TotalMilliseconds
            );
            return new StatusCodeResult(500);
        }
    }
}
