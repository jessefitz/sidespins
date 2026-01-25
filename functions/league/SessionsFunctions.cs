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

public class SessionsFunctions
{
    private readonly ILogger<SessionsFunctions> _logger;
    private readonly LeagueService _cosmosService;
    private readonly IMembershipService _membershipService;

    public SessionsFunctions(
        ILogger<SessionsFunctions> logger,
        LeagueService cosmosService,
        IMembershipService membershipService
    )
    {
        _logger = logger;
        _cosmosService = cosmosService;
        _membershipService = membershipService;
    }

    private async Task<bool> IsCaptainInDivision(FunctionContext context, string divisionId)
    {
        var authUserId = context.GetUserId();
        if (string.IsNullOrEmpty(authUserId))
        {
            return false;
        }

        var memberships = await _membershipService.GetFullMembershipsAsync(authUserId);
        return memberships.Any(m =>
            m.DivisionId == divisionId && (m.Role == "captain" || m.Role == "manager")
        );
    }

    [Function("GetSessions")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetSessions(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "divisions/{divisionId}/sessions"
        )]
            HttpRequest req,
        FunctionContext context,
        string divisionId
    )
    {
        try
        {
            var sessions = await _cosmosService.GetSessionsAsync(divisionId);
            return new OkObjectResult(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sessions");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetActiveSessions")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetActiveSessions(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "divisions/{divisionId}/sessions/active"
        )]
            HttpRequest req,
        FunctionContext context,
        string divisionId
    )
    {
        try
        {
            var sessions = await _cosmosService.GetActiveSessionsAsync(divisionId);
            return new OkObjectResult(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetSession")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetSession(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "divisions/{divisionId}/sessions/{sessionId}"
        )]
            HttpRequest req,
        FunctionContext context,
        string divisionId,
        string sessionId
    )
    {
        try
        {
            var session = await _cosmosService.GetSessionByIdAsync(sessionId, divisionId);
            if (session == null)
            {
                return new NotFoundObjectResult("Session not found");
            }

            return new OkObjectResult(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session");
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateSession")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> CreateSession(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "divisions/{divisionId}/sessions"
        )]
            HttpRequest req,
        FunctionContext context,
        string divisionId
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var session = JsonConvert.DeserializeObject<Session>(requestBody);

            if (session == null)
            {
                return new BadRequestObjectResult("Invalid session data");
            }

            // Log what we received
            _logger.LogInformation(
                $"Received session with DivisionId: '{session.DivisionId}', Route divisionId: '{divisionId}'"
            );

            // Ensure divisionId matches route
            session.DivisionId = divisionId;

            // Validate required fields
            if (string.IsNullOrEmpty(session.Name))
            {
                return new BadRequestObjectResult("Session name is required");
            }

            if (session.StartDate == default(DateTime))
            {
                return new BadRequestObjectResult("Start date is required");
            }

            if (session.EndDate == default(DateTime))
            {
                return new BadRequestObjectResult("End date is required");
            }

            if (session.EndDate <= session.StartDate)
            {
                return new BadRequestObjectResult("End date must be after start date");
            }

            var createdSession = await _cosmosService.CreateSessionAsync(session);
            return new CreatedResult(
                $"/api/divisions/{divisionId}/sessions/{createdSession.Id}",
                createdSession
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateSession")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> UpdateSession(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "divisions/{divisionId}/sessions/{sessionId}"
        )]
            HttpRequest req,
        FunctionContext context,
        string divisionId,
        string sessionId
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var session = JsonConvert.DeserializeObject<Session>(requestBody);

            if (session == null)
            {
                return new BadRequestObjectResult("Invalid session data");
            }

            // Validate required fields
            if (string.IsNullOrEmpty(session.Name))
            {
                return new BadRequestObjectResult("Session name is required");
            }

            if (session.StartDate == default(DateTime))
            {
                return new BadRequestObjectResult("Start date is required");
            }

            if (session.EndDate == default(DateTime))
            {
                return new BadRequestObjectResult("End date is required");
            }

            if (session.EndDate <= session.StartDate)
            {
                return new BadRequestObjectResult("End date must be after start date");
            }

            var updatedSession = await _cosmosService.UpdateSessionAsync(
                sessionId,
                divisionId,
                session
            );
            if (updatedSession == null)
            {
                return new NotFoundObjectResult("Session not found");
            }

            return new OkObjectResult(updatedSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteSession")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> DeleteSession(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "divisions/{divisionId}/sessions/{sessionId}"
        )]
            HttpRequest req,
        FunctionContext context,
        string divisionId,
        string sessionId
    )
    {
        try
        {
            // Check if session has any matches
            var matchCount = await _cosmosService.GetMatchCountBySessionAsync(
                sessionId,
                divisionId
            );
            if (matchCount > 0)
            {
                return new BadRequestObjectResult(
                    $"Cannot delete session with {matchCount} existing matches"
                );
            }

            // Check if any team uses this session as their active session
            var teamsUsingSession = await _cosmosService.GetTeamsUsingActiveSessionAsync(
                sessionId,
                divisionId
            );
            if (teamsUsingSession.Count > 0)
            {
                var teamNames = string.Join(", ", teamsUsingSession.Select(t => t.Name));
                return new BadRequestObjectResult(
                    $"Cannot delete session - it is the active session for: {teamNames}"
                );
            }

            var deleted = await _cosmosService.DeleteSessionAsync(sessionId, divisionId);
            if (!deleted)
            {
                return new NotFoundObjectResult("Session not found");
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session");
            return new StatusCodeResult(500);
        }
    }
}
