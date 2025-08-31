using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Services;
using SideSpins.Api.Models;
using SideSpins.Api.Helpers;
using System.Text.Json;

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
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var matches = await _cosmosService.GetMatchesByDivisionIdAsync(divisionId);
            return new OkObjectResult(matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting matches");
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
            var lineupPlan = JsonSerializer.Deserialize<LineupPlan>(requestBody);
            
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
