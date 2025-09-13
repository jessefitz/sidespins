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

public class MembershipsFunctions
{
    private readonly ILogger<MembershipsFunctions> _logger;
    private readonly LeagueService _cosmosService;

    public MembershipsFunctions(ILogger<MembershipsFunctions> logger, LeagueService cosmosService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
    }

    [Function("GetMemberships")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> GetMemberships(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var teamId = req.Query["teamId"].FirstOrDefault();

            if (string.IsNullOrEmpty(teamId))
            {
                return new BadRequestObjectResult("teamId query parameter is required");
            }

            var memberships = await _cosmosService.GetMembershipsByTeamIdAsync(teamId);
            return new OkObjectResult(memberships);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting memberships");
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateMembership")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> CreateMembership(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var membership = JsonConvert.DeserializeObject<TeamMembership>(requestBody);

            if (
                membership == null
                || string.IsNullOrEmpty(membership.TeamId)
                || string.IsNullOrEmpty(membership.PlayerId)
            )
            {
                return new BadRequestObjectResult(
                    "Invalid membership data - teamId and playerId are required"
                );
            }

            var createdMembership = await _cosmosService.CreateMembershipAsync(membership);
            return new OkObjectResult(createdMembership);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating membership");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateMembership")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> UpdateMembership(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "memberships/{membershipId}")]
            HttpRequest req,
        FunctionContext context,
        string membershipId
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var membership = JsonConvert.DeserializeObject<TeamMembership>(requestBody);

            if (
                membership == null
                || string.IsNullOrEmpty(membership.TeamId)
                || string.IsNullOrEmpty(membership.PlayerId)
            )
            {
                return new BadRequestObjectResult(
                    "Invalid membership data - teamId and playerId are required"
                );
            }

            var updatedMembership = await _cosmosService.UpdateMembershipAsync(
                membershipId,
                membership.TeamId,
                membership
            );
            if (updatedMembership == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(updatedMembership);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating membership {MembershipId}", membershipId);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteMembership")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> DeleteMembership(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "memberships/{membershipId}")]
            HttpRequest req,
        FunctionContext context,
        string membershipId
    )
    {
        try
        {
            // Extract teamId from query parameter since we need it for partition key
            var teamId = req.Query["teamId"].FirstOrDefault();

            if (string.IsNullOrEmpty(teamId))
            {
                return new BadRequestObjectResult(
                    "teamId query parameter is required for deletion"
                );
            }

            var success = await _cosmosService.DeleteMembershipAsync(membershipId, teamId);
            if (!success)
            {
                return new NotFoundResult();
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting membership {MembershipId}", membershipId);
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdatePlayerSkillInFutureLineups")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> UpdatePlayerSkillInFutureLineups(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "memberships/update-future-lineups"
        )]
            HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<UpdateSkillInLineupsRequest>(requestBody);

            if (
                request == null
                || string.IsNullOrEmpty(request.PlayerId)
                || string.IsNullOrEmpty(request.DivisionId)
                || request.NewSkillLevel <= 0
            )
            {
                return new BadRequestObjectResult(
                    "Invalid request - playerId, divisionId, and newSkillLevel are required"
                );
            }

            await _cosmosService.UpdateFutureLineupsForPlayerSkillChangePublicAsync(
                request.PlayerId,
                request.DivisionId,
                request.NewSkillLevel
            );

            return new OkObjectResult(
                new
                {
                    Message = $"Updated skill levels for player {request.PlayerId} in future lineups",
                    PlayerId = request.PlayerId,
                    DivisionId = request.DivisionId,
                    NewSkillLevel = request.NewSkillLevel,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player skill in future lineups");
            return new StatusCodeResult(500);
        }
    }

    // Captain-level membership management endpoints

    [Function("GetTeamMemberships")]
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> GetTeamMemberships(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams/{teamId}/memberships")]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            var memberships = await _cosmosService.GetMembershipsByTeamIdAsync(teamId);
            return new OkObjectResult(memberships);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team memberships for team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateTeamMembership")]
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> CreateTeamMembership(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teams/{teamId}/memberships")]
            HttpRequest req,
        FunctionContext context,
        string teamId
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var membership = JsonConvert.DeserializeObject<TeamMembership>(requestBody);

            if (membership == null || string.IsNullOrEmpty(membership.PlayerId))
            {
                return new BadRequestObjectResult("Invalid membership data - playerId is required");
            }

            // Override teamId from route
            membership.TeamId = teamId;

            var createdMembership = await _cosmosService.CreateMembershipAsync(membership);
            return new OkObjectResult(createdMembership);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team membership for team {TeamId}", teamId);
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateTeamMembership")]
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> UpdateTeamMembership(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "teams/{teamId}/memberships/{membershipId}"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamId,
        string membershipId
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var membership = JsonConvert.DeserializeObject<TeamMembership>(requestBody);

            if (membership == null || string.IsNullOrEmpty(membership.PlayerId))
            {
                return new BadRequestObjectResult("Invalid membership data - playerId is required");
            }

            // Override teamId from route
            membership.TeamId = teamId;

            var updatedMembership = await _cosmosService.UpdateMembershipAsync(
                membershipId,
                teamId,
                membership
            );
            if (updatedMembership == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(updatedMembership);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating membership {MembershipId} for team {TeamId}",
                membershipId,
                teamId
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteTeamMembership")]
    [RequireAuthentication]
    [RequireTeamRole("captain")]
    public async Task<IActionResult> DeleteTeamMembership(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "teams/{teamId}/memberships/{membershipId}"
        )]
            HttpRequest req,
        FunctionContext context,
        string teamId,
        string membershipId
    )
    {
        try
        {
            var success = await _cosmosService.DeleteMembershipAsync(membershipId, teamId);
            if (!success)
            {
                return new NotFoundResult();
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting membership {MembershipId} for team {TeamId}",
                membershipId,
                teamId
            );
            return new StatusCodeResult(500);
        }
    }
}

public class UpdateSkillInLineupsRequest
{
    [JsonProperty("playerId")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonProperty("divisionId")]
    public string DivisionId { get; set; } = string.Empty;

    [JsonProperty("newSkillLevel")]
    public int NewSkillLevel { get; set; }
}
