using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SideSpins.Api.Helpers;
using SideSpins.Api.Models;
using SideSpins.Api.Services;
using SidesSpins.Functions;

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
    [RequireAuthentication]
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

    [Function("GetTeamDetails")]
    [RequireAuthentication]
    [RequireTeamRole("player")]
    public async Task<IActionResult> GetTeamDetails(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams/{teamId}")]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            var membership = (UserTeamMembership?)context.Items["ActiveMembership"];
            if (membership == null)
            {
                return new StatusCodeResult(403);
            }

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

            return new OkObjectResult(new { team = team, userRole = membership.Role });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team details for {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateTeam")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> CreateTeam(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var team = JsonConvert.DeserializeObject<SideSpins.Api.Models.Team>(requestBody);

            if (team == null)
            {
                return new BadRequestObjectResult("Invalid team data");
            }

            // Validate required fields
            if (string.IsNullOrEmpty(team.DivisionId) || string.IsNullOrEmpty(team.Name))
            {
                return new BadRequestObjectResult("DivisionId and Name are required");
            }

            // Check if user is authorized to create a team (managers and admins only)
            var userClaims = context.GetUserClaims();
            if (userClaims == null)
            {
                return new UnauthorizedResult();
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
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> UpdateTeam(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "teams/{teamId}")]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult(
                    "divisionId query parameter is required for team updates"
                );
            }

            // Authorization is handled by the RequireTeamRole attribute
            var membership = (UserTeamMembership?)context.Items["ActiveMembership"];

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var team = JsonConvert.DeserializeObject<SideSpins.Api.Models.Team>(requestBody);

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
    [RequireAuthentication("admin")]
    public async Task<IActionResult> DeleteTeam(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "teams/{teamId}")]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            // Extract divisionId from query parameter since we need it for partition key
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult(
                    "divisionId query parameter is required for team deletion"
                );
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
    [RequireAuthentication]
    [RequireTeamRole("player")]
    public async Task<IActionResult> GetTeam(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams/{teamId}/details")]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            // Authorization is handled by the RequireTeamRole attribute
            var membership = (UserTeamMembership?)context.Items["ActiveMembership"];

            var team = await _cosmosService.GetTeamByIdAsync(teamId, divisionId);
            if (team == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(new { team = team, userRole = membership?.Role });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Updates the active session for a team. Captains can only select from active sessions in their division.
    /// </summary>
    [Function("UpdateTeamActiveSession")]
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> UpdateTeamActiveSession(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "teams/{teamId}/active-session"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            var divisionId = req.Query["divisionId"].FirstOrDefault();
            if (string.IsNullOrEmpty(divisionId))
            {
                return new BadRequestObjectResult("divisionId query parameter is required");
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var payload = JsonConvert.DeserializeObject<UpdateActiveSessionRequest>(requestBody);

            if (payload == null)
            {
                return new BadRequestObjectResult("Invalid request body");
            }

            // If sessionId is provided, validate it exists and is active
            if (!string.IsNullOrEmpty(payload.SessionId))
            {
                var session = await _cosmosService.GetSessionByIdAsync(
                    payload.SessionId,
                    divisionId
                );
                if (session == null)
                {
                    return new NotFoundObjectResult("Session not found");
                }
                if (!session.IsActive)
                {
                    return new BadRequestObjectResult(
                        "Cannot set inactive session as team's active session"
                    );
                }
            }

            var updatedTeam = await _cosmosService.UpdateTeamActiveSessionAsync(
                teamId,
                divisionId,
                payload.SessionId
            );

            if (updatedTeam == null)
            {
                return new NotFoundObjectResult("Team not found");
            }

            return new OkObjectResult(updatedTeam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating active session for team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }
}

/// <summary>
/// Request model for updating team's active session
/// </summary>
public class UpdateActiveSessionRequest
{
    [JsonProperty("sessionId")]
    public string? SessionId { get; set; }
}
