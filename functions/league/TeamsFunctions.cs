using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SideSpins.Api.Helpers;
using SideSpins.Api.Models;
using SideSpins.Api.Services;

namespace SideSpins.Api.Functions;

public class TeamsFunctions
{
    private readonly ILogger<TeamsFunctions> _logger;
    private readonly LeagueService _cosmosService;

    public TeamsFunctions(ILogger<TeamsFunctions> logger, LeagueService cosmosService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
    }

    [Function("GetTeams")]
    public async Task<IActionResult> GetTeams(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var teams = await _cosmosService.GetTeamsByDivisionIdAsync(divisionId);
            return new OkObjectResult(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting teams");
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateTeam")]
    public async Task<IActionResult> CreateTeam(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req
    )
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var team = JsonConvert.DeserializeObject<Team>(requestBody);
            
            if (team == null)
            {
                return new BadRequestObjectResult("Invalid team data");
            }

            // Validate required fields
            if (string.IsNullOrEmpty(team.DivisionId) || string.IsNullOrEmpty(team.Name))
            {
                return new BadRequestObjectResult("DivisionId and Name are required");
            }

            var createdTeam = await _cosmosService.CreateTeamAsync(team);
            return new CreatedResult($"/api/teams/{createdTeam.Id}", createdTeam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateTeam")]
    public async Task<IActionResult> UpdateTeam([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "teams/{teamId}")] HttpRequest req, string teamId)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required for team updates");
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var team = JsonConvert.DeserializeObject<Team>(requestBody);
            
            if (team == null || string.IsNullOrEmpty(team.Name))
            {
                return new BadRequestObjectResult("Invalid team data - name is required");
            }

            var updatedTeam = await _cosmosService.UpdateTeamAsync(teamId, divisionId, team);
            if (updatedTeam == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(updatedTeam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteTeam")]
    public async Task<IActionResult> DeleteTeam([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "teams/{teamId}")] HttpRequest req, string teamId)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required for team deletion");
            }

            var success = await _cosmosService.DeleteTeamAsync(teamId, divisionId);
            if (!success)
            {
                return new NotFoundResult();
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }

    [Function("GetTeam")]
    public async Task<IActionResult> GetTeam([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams/{teamId}")] HttpRequest req, string teamId)
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var team = await _cosmosService.GetTeamByIdAsync(teamId, divisionId);
            if (team == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(team);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }
}
