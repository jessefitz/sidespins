using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Models;
using SideSpins.Api.Services;

namespace SidesSpins.Functions;

public class MigrationFunctions
{
    private readonly ILogger<MigrationFunctions> _logger;
    private readonly LeagueService _leagueService;
    private readonly CosmosClient _cosmosClient;

    public MigrationFunctions(
        ILogger<MigrationFunctions> logger,
        LeagueService leagueService,
        CosmosClient cosmosClient
    )
    {
        _logger = logger;
        _leagueService = leagueService;
        _cosmosClient = cosmosClient;
    }

    [Function("MigratePlayersAddAuthUserId")]
    [RequireAuthentication("admin")]
    public async Task<IActionResult> MigratePlayersAddAuthUserId(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "admin/migrate/players/auth-userid"
        )]
            HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            _logger.LogInformation("Starting migration to add authUserId to player documents");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var migrationData = JsonSerializer.Deserialize<List<PlayerAuthMapping>>(requestBody);

            if (migrationData == null || !migrationData.Any())
            {
                return new BadRequestObjectResult(
                    "Migration data is required. Expected format: [{ \"playerId\": \"p_brian\", \"authUserId\": \"auth-id-123\" }]"
                );
            }

            var results = new List<MigrationResult>();
            var databaseName = Environment.GetEnvironmentVariable("COSMOS_DB") ?? "sidespins";
            var container = _cosmosClient.GetContainer(databaseName, "Players");

            foreach (var mapping in migrationData)
            {
                try
                {
                    // Get the existing player document
                    var response = await container.ReadItemAsync<Player>(
                        mapping.PlayerId,
                        new PartitionKey(mapping.PlayerId)
                    );
                    var player = response.Resource;

                    if (player == null)
                    {
                        results.Add(
                            new MigrationResult
                            {
                                PlayerId = mapping.PlayerId,
                                Success = false,
                                Message = "Player not found",
                            }
                        );
                        continue;
                    }

                    // Update with authUserId
                    player.AuthUserId = mapping.AuthUserId;

                    // Save the updated document
                    await container.ReplaceItemAsync(
                        player,
                        mapping.PlayerId,
                        new PartitionKey(mapping.PlayerId)
                    );

                    results.Add(
                        new MigrationResult
                        {
                            PlayerId = mapping.PlayerId,
                            Success = true,
                            Message = $"Successfully added authUserId: {mapping.AuthUserId}",
                        }
                    );

                    _logger.LogInformation(
                        "Updated player {PlayerId} with authUserId {AuthUserId}",
                        mapping.PlayerId,
                        mapping.AuthUserId
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating player {PlayerId}", mapping.PlayerId);
                    results.Add(
                        new MigrationResult
                        {
                            PlayerId = mapping.PlayerId,
                            Success = false,
                            Message = $"Error: {ex.Message}",
                        }
                    );
                }
            }

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count(r => !r.Success);

            _logger.LogInformation(
                "Migration completed. Success: {SuccessCount}, Failures: {FailureCount}",
                successCount,
                failureCount
            );

            return new OkObjectResult(
                new
                {
                    summary = new
                    {
                        totalProcessed = results.Count,
                        successful = successCount,
                        failed = failureCount,
                    },
                    results = results,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during player authUserId migration");
            return new StatusCodeResult(500);
        }
    }
}

public record PlayerAuthMapping(string PlayerId, string AuthUserId);

public record MigrationResult
{
    public string PlayerId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
