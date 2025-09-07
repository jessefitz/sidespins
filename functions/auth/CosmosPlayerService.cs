using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Models;
using System.Net;

namespace SidesSpins.Functions;

/// <summary>
/// Cosmos DB implementation of the player service
/// </summary>
public class CosmosPlayerService : IPlayerService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<CosmosPlayerService> _logger;
    private readonly string _databaseName;
    private readonly string _containerName;

    public CosmosPlayerService(
        CosmosClient cosmosClient, 
        ILogger<CosmosPlayerService> logger,
        string databaseName = "sidespins",
        string containerName = "Players"
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        _databaseName = databaseName;
        _containerName = containerName;
    }

    public async Task<Player?> GetPlayerByAuthUserIdAsync(string authUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.authUserId = @authUserId AND c.type = @type"
            )
            .WithParameter("@authUserId", authUserId)
            .WithParameter("@type", "player");

            var iterator = container.GetItemQueryIterator<Player>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var player = response.FirstOrDefault();
                if (player != null)
                {
                    _logger.LogInformation("Found player {PlayerId} for authUserId {AuthUserId}", player.Id, authUserId);
                    return player;
                }
            }
            
            _logger.LogWarning("No player found for authUserId {AuthUserId}", authUserId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving player by authUserId {AuthUserId}", authUserId);
            throw;
        }
    }

    public async Task<Player?> GetPlayerByIdAsync(string playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            var response = await container.ReadItemAsync<Player>(playerId, new PartitionKey(playerId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Player {PlayerId} not found", playerId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving player {PlayerId}", playerId);
            throw;
        }
    }

    public async Task<List<Player>> GetPlayersByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.phoneNumber = @phoneNumber AND c.type = @type"
            )
            .WithParameter("@phoneNumber", phoneNumber)
            .WithParameter("@type", "player");

            var iterator = container.GetItemQueryIterator<Player>(query);
            var players = new List<Player>();
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                players.AddRange(response);
            }
            
            _logger.LogInformation("Found {Count} players with phone number {PhoneNumber}", players.Count, phoneNumber);
            return players;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving players by phone number {PhoneNumber}", phoneNumber);
            throw;
        }
    }

    public async Task<Player> UpdatePlayerAsync(Player player, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            
            _logger.LogInformation("Updating player {PlayerId} with authUserId {AuthUserId}", player.Id, player.AuthUserId);
            
            var response = await container.UpsertItemAsync(player, new PartitionKey(player.Id), cancellationToken: cancellationToken);
            
            _logger.LogInformation("Player update completed with status {StatusCode}", response.StatusCode);
            
            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId}", player.Id);
            throw;
        }
    }
}
