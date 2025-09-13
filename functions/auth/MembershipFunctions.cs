using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace SidesSpins.Functions;

public class MembershipFunctions
{
    private readonly ILogger<MembershipFunctions> _logger;
    private readonly IMembershipService _membershipService;
    private readonly IPlayerService _playerService;

    public MembershipFunctions(
        ILogger<MembershipFunctions> logger,
        IMembershipService membershipService,
        IPlayerService playerService
    )
    {
        _logger = logger;
        _membershipService = membershipService;
        _playerService = playerService;
    }

    [Function("GetMyMemberships")]
    [RequireAuthentication]
    public async Task<IActionResult> GetMyMemberships(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/memberships")]
            HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var userId = context.Items["UserId"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return new UnauthorizedResult();
            }

            _logger.LogInformation("Looking up player for authUserId: {AuthUserId}", userId);

            // First, find the player by their auth user ID
            var player = await _playerService.GetPlayerByAuthUserIdAsync(userId);
            if (player == null)
            {
                _logger.LogWarning("No player found for authUserId: {AuthUserId}", userId);
                return new OkObjectResult(
                    new
                    {
                        memberships = new List<object>(),
                        count = 0,
                        message = "No player profile found for this user",
                    }
                );
            }

            _logger.LogInformation(
                "Found player {PlayerId} for authUserId {AuthUserId}",
                player.Id,
                userId
            );

            // Now get memberships using the player ID
            var memberships = await _membershipService.GetAllAsync(player.AuthUserId);

            var membershipInfos = memberships
                .Select(m => new
                {
                    teamId = m.TeamId,
                    role = m.Role,
                    active = m.Active,
                })
                .ToList();

            _logger.LogInformation(
                "Found {Count} memberships for player {PlayerId}",
                membershipInfos.Count,
                player.Id
            );

            return new OkObjectResult(
                new { memberships = membershipInfos, count = membershipInfos.Count }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user memberships");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetMyProfile")]
    [RequireAuthentication]
    public async Task<IActionResult> GetMyProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/profile")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var userId = context.Items["UserId"]?.ToString();
            var sidespinsRole = context.Items["SidespinsRole"]?.ToString();

            if (string.IsNullOrEmpty(userId))
            {
                return new UnauthorizedResult();
            }

            _logger.LogInformation("Looking up player for authUserId: {AuthUserId}", userId);

            // First, find the player by their auth user ID
            var player = await _playerService.GetPlayerByAuthUserIdAsync(userId);
            if (player == null)
            {
                _logger.LogWarning("No player found for authUserId: {AuthUserId}", userId);
                return new OkObjectResult(
                    new
                    {
                        userId = userId,
                        sidespinsRole = sidespinsRole ?? "member",
                        memberships = new List<object>(),
                        message = "No player profile found for this user",
                        debug = new { authUserId = userId, playerFound = false },
                    }
                );
            }

            // Now get memberships using the player ID
            var memberships = await _membershipService.GetAllAsync(player.Id);

            var membershipInfos = memberships
                .Select(m => new
                {
                    teamId = m.TeamId,
                    role = m.Role,
                    active = m.Active,
                })
                .ToList();

            return new OkObjectResult(
                new
                {
                    userId = userId,
                    playerId = player.Id,
                    sidespinsRole = sidespinsRole ?? "member",
                    memberships = membershipInfos,
                    debug = new
                    {
                        authUserId = userId,
                        playerFound = true,
                        playerInfo = new
                        {
                            id = player.Id,
                            firstName = player.FirstName,
                            lastName = player.LastName,
                            apaNumber = player.ApaNumber,
                            phoneNumber = player.PhoneNumber,
                        },
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile");
            return new StatusCodeResult(500);
        }
    }
}
