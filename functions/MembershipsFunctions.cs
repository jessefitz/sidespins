using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Services;
using SideSpins.Api.Models;
using SideSpins.Api.Helpers;
using System.Text.Json;

namespace SideSpins.Api;

public class MembershipsFunctions
{
    private readonly ILogger<MembershipsFunctions> _logger;
    private readonly CosmosService _cosmosService;

    public MembershipsFunctions(ILogger<MembershipsFunctions> logger, CosmosService cosmosService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
    }

    [Function("GetMemberships")]
    public async Task<IActionResult> GetMemberships([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

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
    public async Task<IActionResult> CreateMembership([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var membership = JsonSerializer.Deserialize<TeamMembership>(requestBody);
            
            if (membership == null || string.IsNullOrEmpty(membership.TeamId) || string.IsNullOrEmpty(membership.PlayerId))
            {
                return new BadRequestObjectResult("Invalid membership data - teamId and playerId are required");
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

    [Function("DeleteMembership")]
    public async Task<IActionResult> DeleteMembership([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "memberships/{membershipId}")] HttpRequest req, string membershipId)
    {
        var authResult = AuthHelper.ValidateApiSecret(req);
        if (authResult != null) return authResult;

        try
        {
            // Extract teamId from query parameter since we need it for partition key
            var teamId = req.Query["teamId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(teamId))
            {
                return new BadRequestObjectResult("teamId query parameter is required for deletion");
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
}
