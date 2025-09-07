using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Services;
using System.Text.Json;

namespace SidesSpins.Functions;

public class PlayerManagementFunctions
{
    private readonly ILogger<PlayerManagementFunctions> _logger;
    private readonly LeagueService _leagueService;
    private readonly IPlayerService _playerService;

    public PlayerManagementFunctions(
        ILogger<PlayerManagementFunctions> logger,
        LeagueService leagueService,
        IPlayerService playerService)
    {
        _logger = logger;
        _leagueService = leagueService;
        _playerService = playerService;
    }

    [Function("LinkPlayerToAuthUser")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> LinkPlayerToAuthUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/players/{playerId}/link-auth")] HttpRequest req,
        FunctionContext context,
        string playerId
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var linkRequest = JsonSerializer.Deserialize<LinkPlayerAuthRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (linkRequest == null || string.IsNullOrEmpty(linkRequest.AuthUserId))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "AuthUserId is required"
                });
            }

            // Get the player
            var player = await _leagueService.GetPlayerByIdAsync(playerId);
            if (player == null)
            {
                return new NotFoundObjectResult(new
                {
                    success = false,
                    message = $"Player {playerId} not found"
                });
            }

            // Check if authUserId is already in use
            var existingPlayer = await _playerService.GetPlayerByAuthUserIdAsync(linkRequest.AuthUserId);
            if (existingPlayer != null && existingPlayer.Id != playerId)
            {
                return new ConflictObjectResult(new
                {
                    success = false,
                    message = $"AuthUserId {linkRequest.AuthUserId} is already linked to player {existingPlayer.Id} ({existingPlayer.FirstName} {existingPlayer.LastName})"
                });
            }

            // Update the player with authUserId
            player.AuthUserId = linkRequest.AuthUserId;
            var updatedPlayer = await _leagueService.UpdatePlayerAsync(playerId, player);

            if (updatedPlayer == null)
            {
                return new StatusCodeResult(500);
            }

            _logger.LogInformation("Admin linked authUserId {AuthUserId} to player {PlayerId} ({FirstName} {LastName})", 
                linkRequest.AuthUserId, playerId, player.FirstName, player.LastName);

            return new OkObjectResult(new
            {
                success = true,
                message = $"Successfully linked authUserId {linkRequest.AuthUserId} to player {playerId}",
                player = new
                {
                    id = updatedPlayer.Id,
                    firstName = updatedPlayer.FirstName,
                    lastName = updatedPlayer.LastName,
                    authUserId = updatedPlayer.AuthUserId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking player {PlayerId} to authUserId", playerId);
            return new StatusCodeResult(500);
        }
    }

    [Function("UnlinkPlayerFromAuthUser")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> UnlinkPlayerFromAuthUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "admin/players/{playerId}/link-auth")] HttpRequest req,
        FunctionContext context,
        string playerId
    )
    {
        try
        {
            // Get the player
            var player = await _leagueService.GetPlayerByIdAsync(playerId);
            if (player == null)
            {
                return new NotFoundObjectResult(new
                {
                    success = false,
                    message = $"Player {playerId} not found"
                });
            }

            var oldAuthUserId = player.AuthUserId;
            
            // Remove the authUserId
            player.AuthUserId = null;
            var updatedPlayer = await _leagueService.UpdatePlayerAsync(playerId, player);

            if (updatedPlayer == null)
            {
                return new StatusCodeResult(500);
            }

            _logger.LogInformation("Admin unlinked authUserId {AuthUserId} from player {PlayerId} ({FirstName} {LastName})", 
                oldAuthUserId, playerId, player.FirstName, player.LastName);

            return new OkObjectResult(new
            {
                success = true,
                message = $"Successfully unlinked player {playerId} from authUserId {oldAuthUserId}",
                player = new
                {
                    id = updatedPlayer.Id,
                    firstName = updatedPlayer.FirstName,
                    lastName = updatedPlayer.LastName,
                    authUserId = updatedPlayer.AuthUserId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking player {PlayerId} from authUserId", playerId);
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPlayersWithoutAuth")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> GetPlayersWithoutAuth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/players/without-auth")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var players = await _leagueService.GetPlayersAsync();
            var playersWithoutAuth = players
                .Where(p => string.IsNullOrEmpty(p.AuthUserId))
                .Select(p => new
                {
                    id = p.Id,
                    firstName = p.FirstName,
                    lastName = p.LastName,
                    apaNumber = p.ApaNumber,
                    phoneNumber = p.PhoneNumber,
                    createdAt = p.CreatedAt
                })
                .OrderBy(p => p.lastName)
                .ThenBy(p => p.firstName)
                .ToList();

            return new OkObjectResult(new
            {
                success = true,
                count = playersWithoutAuth.Count,
                players = playersWithoutAuth
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving players without auth");
            return new StatusCodeResult(500);
        }
    }
}

public record LinkPlayerAuthRequest(string AuthUserId);
