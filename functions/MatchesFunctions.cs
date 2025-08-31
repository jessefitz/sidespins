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
    private readonly CosmosService _cosmosService;

    public MatchesFunctions(ILogger<MatchesFunctions> logger, CosmosService cosmosService)
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
}
