using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Models;

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

    public async Task<Player?> GetPlayerByAuthUserIdAsync(
        string authUserId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation(
                "[CosmosPlayerService] Starting GetPlayerByAuthUserIdAsync for authUserId: '{AuthUserId}'",
                authUserId
            );
            _logger.LogInformation(
                "[CosmosPlayerService] AuthUserId details - Length: {Length}, IsNullOrEmpty: {IsEmpty}",
                authUserId?.Length,
                string.IsNullOrEmpty(authUserId)
            );

            if (string.IsNullOrEmpty(authUserId))
            {
                _logger.LogWarning("[CosmosPlayerService] AuthUserId is null or empty");
                return null;
            }

            var container = _cosmosClient.GetContainer(_databaseName, _containerName);

            var queryText = "SELECT * FROM c WHERE c.authUserId = @authUserId AND c.type = @type";
            _logger.LogInformation("[CosmosPlayerService] Executing query: {Query}", queryText);
            _logger.LogInformation(
                "[CosmosPlayerService] Query parameters - @authUserId: '{AuthUserId}', @type: 'player'",
                authUserId
            );

            var query = new QueryDefinition(queryText)
                .WithParameter("@authUserId", authUserId)
                .WithParameter("@type", "player");

            var iterator = container.GetItemQueryIterator<Player>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                _logger.LogInformation(
                    "[CosmosPlayerService] Query returned {Count} player records",
                    response.Count
                );

                var player = response.FirstOrDefault();
                if (player != null)
                {
                    _logger.LogInformation(
                        "[CosmosPlayerService] Found player - Id: '{PlayerId}', FirstName: '{FirstName}', AuthUserId: '{AuthUserId}'",
                        player.Id,
                        player.FirstName,
                        player.AuthUserId
                    );
                    return player;
                }
            }

            _logger.LogWarning(
                "[CosmosPlayerService] No player found for authUserId: '{AuthUserId}'",
                authUserId
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[CosmosPlayerService] Error retrieving player by authUserId: '{AuthUserId}'",
                authUserId
            );
            throw;
        }
    }

    public async Task<Player?> GetPlayerByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            var response = await container.ReadItemAsync<Player>(
                playerId,
                new PartitionKey(playerId),
                cancellationToken: cancellationToken
            );
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

    public async Task<List<Player>> GetPlayersByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default
    )
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

            _logger.LogInformation(
                "Found {Count} players with phone number {PhoneNumber}",
                players.Count,
                phoneNumber
            );
            return players;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving players by phone number {PhoneNumber}",
                phoneNumber
            );
            throw;
        }
    }

    public async Task<Player> UpdatePlayerAsync(
        Player player,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);

            _logger.LogInformation(
                "Updating player {PlayerId} with authUserId {AuthUserId}",
                player.Id,
                player.AuthUserId
            );

            var response = await container.UpsertItemAsync(
                player,
                new PartitionKey(player.Id),
                cancellationToken: cancellationToken
            );

            _logger.LogInformation(
                "Player update completed with status {StatusCode}",
                response.StatusCode
            );

            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId}", player.Id);
            throw;
        }
    }

    public async Task<Player?> GetPlayerByApaNumberAsync(
        string apaNumber,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrEmpty(apaNumber))
            {
                _logger.LogWarning("[CosmosPlayerService] APA number is null or empty");
                return null;
            }

            var container = _cosmosClient.GetContainer(_databaseName, _containerName);

            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.apaNumber = @apaNumber AND c.type = @type"
            )
                .WithParameter("@apaNumber", apaNumber)
                .WithParameter("@type", "player");

            var iterator = container.GetItemQueryIterator<Player>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var player = response.FirstOrDefault();
                if (player != null)
                {
                    _logger.LogInformation(
                        "[CosmosPlayerService] Found player with APA number {ApaNumber}: {PlayerId}",
                        apaNumber,
                        player.Id
                    );
                    return player;
                }
            }

            _logger.LogInformation(
                "[CosmosPlayerService] No player found with APA number {ApaNumber}",
                apaNumber
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[CosmosPlayerService] Error retrieving player by APA number {ApaNumber}",
                apaNumber
            );
            throw;
        }
    }

    public async Task<bool> IsApaNumberAlreadyRegisteredAsync(
        string apaNumber,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var player = await GetPlayerByApaNumberAsync(apaNumber, cancellationToken);
            var isRegistered = player != null && !string.IsNullOrEmpty(player.AuthUserId);

            _logger.LogInformation(
                "[CosmosPlayerService] APA number {ApaNumber} registration status: {IsRegistered}",
                apaNumber,
                isRegistered
            );

            return isRegistered;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[CosmosPlayerService] Error checking APA number registration status for {ApaNumber}",
                apaNumber
            );
            throw;
        }
    }

    public async Task<bool> IsPhoneNumberAlreadyRegisteredAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var players = await GetPlayersByPhoneNumberAsync(phoneNumber, cancellationToken);
            var isRegistered = players.Any(p => !string.IsNullOrEmpty(p.AuthUserId));

            _logger.LogInformation(
                "[CosmosPlayerService] Phone number {PhoneNumber} registration status: {IsRegistered}",
                phoneNumber,
                isRegistered
            );

            return isRegistered;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[CosmosPlayerService] Error checking phone number registration status for {PhoneNumber}",
                phoneNumber
            );
            throw;
        }
    }
}
