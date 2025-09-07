using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Services;
using SideSpins.Api.Models;
using SideSpins.Api.Helpers;
using Newtonsoft.Json;
using SidesSpins.Functions;

namespace SideSpins.Api;

public class PlayersFunctions
{
    private readonly ILogger<PlayersFunctions> _logger;
    private readonly LeagueService _cosmosService;

    public PlayersFunctions(ILogger<PlayersFunctions> logger, LeagueService cosmosService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
    }

    [Function("GetPlayers")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetPlayers([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, FunctionContext context)
    {
        try
        {
            // For now, allow any authenticated user to see all players
            // You could add team-specific filtering here if needed
            var players = await _cosmosService.GetPlayersAsync();
            return new OkObjectResult(players);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting players");
            return new StatusCodeResult(500);
        }
    }

    [Function("CreatePlayer")]
   [RequireAuthentication("admin")]
    public async Task<IActionResult> CreatePlayer([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, FunctionContext context)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var player = JsonConvert.DeserializeObject<Player>(requestBody);
            
            if (player == null || string.IsNullOrEmpty(player.FirstName))
            {
                return new BadRequestObjectResult("Invalid player data");
            }

            var createdPlayer = await _cosmosService.CreatePlayerAsync(player);
            return new OkObjectResult(createdPlayer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating player");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdatePlayer")]
   [RequireAuthentication("admin")]
    public async Task<IActionResult> UpdatePlayer([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "players/{id}")] HttpRequest req, FunctionContext context, string id)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Updating player {PlayerId} with data: {RequestBody}", id, requestBody);
            
            var player = JsonConvert.DeserializeObject<Player>(requestBody);
            
            if (player == null)
            {
                _logger.LogWarning("Failed to deserialize player data for {PlayerId}", id);
                return new BadRequestObjectResult("Invalid player data");
            }

            _logger.LogInformation("Deserialized player: Id={PlayerId}, FirstName={FirstName}, LastName={LastName}", 
                player.Id, player.FirstName, player.LastName);

            var updatedPlayer = await _cosmosService.UpdatePlayerAsync(id, player);
            if (updatedPlayer == null)
            {
                _logger.LogWarning("Player {PlayerId} not found for update", id);
                return new NotFoundResult();
            }

            _logger.LogInformation("Successfully updated player {PlayerId}", id);
            return new OkObjectResult(updatedPlayer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId}: {ErrorMessage}", id, ex.Message);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeletePlayer")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> DeletePlayer([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "players/{id}")] HttpRequest req, FunctionContext context, string id)
    {
        try
        {
            var success = await _cosmosService.DeletePlayerAsync(id);
            if (!success)
            {
                return new NotFoundResult();
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting player {PlayerId}", id);
            return new StatusCodeResult(500);
        }
    }
}