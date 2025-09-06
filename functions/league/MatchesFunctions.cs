using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Services;
using SideSpins.Api.Models;
using SideSpins.Api.Helpers;
using Newtonsoft.Json;

namespace SideSpins.Api;

public class MatchesFunctions
{
    private readonly ILogger<MatchesFunctions> _logger;
    private readonly LeagueService _cosmosService;

    public MatchesFunctions(ILogger<MatchesFunctions> logger, LeagueService cosmosService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
    }

    [Function("GetMatches")]
    public async Task<IActionResult> GetMatches([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
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
                if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
                {
                    return new BadRequestObjectResult("Invalid date format. Use ISO 8601 format (YYYY-MM-DD)");
                }
                matches = await _cosmosService.GetMatchesByDateRangeAsync(divisionId, start, end.AddDays(1)); // Include end date
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
    public async Task<IActionResult> CreateMatch([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "matches")] HttpRequest req)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var match = JsonConvert.DeserializeObject<TeamMatch>(requestBody);
            
            if (match == null)
            {
                return new BadRequestObjectResult("Invalid match data");
            }

            // Validate required fields
            if (string.IsNullOrEmpty(match.DivisionId) || match.Week <= 0 || match.ScheduledAt == default(DateTime))
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
    public async Task<IActionResult> UpdateMatch([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "matches/{matchId}")] HttpRequest req, string matchId)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required for match updates");
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
    public async Task<IActionResult> DeleteMatch([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "matches/{matchId}")] HttpRequest req, string matchId)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required for match deletion");
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
    public async Task<IActionResult> UpdateMatchLineup([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "matches/{matchId}/lineup")] HttpRequest req, string matchId)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required for match updates");
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var lineupPlan = JsonConvert.DeserializeObject<LineupPlan>(requestBody);
            
            if (lineupPlan == null)
            {
                return new BadRequestObjectResult("Invalid lineup data");
            }

            var updatedMatch = await _cosmosService.UpdateMatchLineupAsync(matchId, divisionId, lineupPlan);
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

    // Public endpoints for the lineup sandbox
    [Function("GetPublicMatches")]
    public async Task<IActionResult> GetPublicMatches([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/matches")] HttpRequest req)
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
                lineupPlan = m.LineupPlan
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
    public async Task<IActionResult> GetPublicMatchRoster([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/matches/{matchId}/roster")] HttpRequest req, string matchId)
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
                var homeMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(match.HomeTeamId);
                var homePlayers = await GetPlayersWithSkills(homeMemberships);
                roster.AddRange(homePlayers);
            }

            if (!string.IsNullOrEmpty(match.AwayTeamId) && match.AwayTeamId != match.HomeTeamId)
            {
                var awayMemberships = await _cosmosService.GetMembershipsByTeamIdAsync(match.AwayTeamId);
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
                playersWithSkills.Add(new
                {
                    playerId = player.Id,
                    name = $"{player.FirstName} {player.LastName}".Trim(),
                    skill = membership.SkillLevel_9b ?? 0
                });
            }
        }

        return playersWithSkills;
    }
}
