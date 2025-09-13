using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Models;
using SidesSpins.Functions;

namespace SidesSpins.Functions;

/// <summary>
/// Cosmos DB implementation of the membership service
/// </summary>
public class CosmosMembershipService : IMembershipService
{
    private readonly CosmosClient _cosmosClient;
    private readonly IPlayerService _playerService;
    private readonly ILogger<CosmosMembershipService> _logger;
    private readonly string _databaseName;
    private readonly string _containerName;

    public CosmosMembershipService(
        CosmosClient cosmosClient,
        IPlayerService playerService,
        ILogger<CosmosMembershipService> logger,
        string databaseName = "sidespins",
        string containerName = "league"
    )
    {
        _cosmosClient = cosmosClient;
        _playerService = playerService;
        _logger = logger;
        _databaseName = databaseName;
        _containerName = containerName;
    }

    public async Task<UserTeamMembership?> GetAsync(
        string authUserId,
        string teamId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation(
                "[CosmosMembershipService] Starting GetAsync for authUserId: '{AuthUserId}', teamId: '{TeamId}'",
                authUserId,
                teamId
            );
            _logger.LogInformation(
                "[CosmosMembershipService] AuthUserId length: {Length}, IsNullOrEmpty: {IsEmpty}",
                authUserId?.Length,
                string.IsNullOrEmpty(authUserId)
            );

            if (string.IsNullOrEmpty(authUserId))
            {
                _logger.LogWarning("[CosmosMembershipService] AuthUserId is null or empty");
                return null;
            }

            // First, map the auth user ID to player ID
            var player = await _playerService.GetPlayerByAuthUserIdAsync(
                authUserId,
                cancellationToken
            );
            if (player == null)
            {
                _logger.LogWarning(
                    "[CosmosMembershipService] No player found for auth user ID {AuthUserId}",
                    authUserId
                );
                return null;
            }

            _logger.LogInformation(
                "[CosmosMembershipService] Found player: Id='{PlayerId}', FirstName='{FirstName}' for authUserId: '{AuthUserId}'",
                player.Id,
                player.FirstName,
                authUserId
            );

            var container = _cosmosClient.GetContainer(_databaseName, _containerName);

            // Query for active membership for the specific player and team
            var queryText =
                "SELECT * FROM c WHERE c.playerId = @playerId AND c.teamId = @teamId AND (c.leftAt = null OR NOT IS_DEFINED(c.leftAt))";
            _logger.LogInformation("[CosmosMembershipService] Executing query: {Query}", queryText);
            _logger.LogInformation(
                "[CosmosMembershipService] Query parameters - @playerId: '{PlayerId}', @teamId: '{TeamId}'",
                player.Id,
                teamId
            );

            var query = new QueryDefinition(queryText)
                .WithParameter("@playerId", player.Id)
                .WithParameter("@teamId", teamId);

            var iterator = container.GetItemQueryIterator<TeamMembership>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                _logger.LogInformation(
                    "[CosmosMembershipService] Query returned {Count} membership records",
                    response.Count
                );

                var membership = response.FirstOrDefault();

                if (membership != null)
                {
                    _logger.LogInformation(
                        "[CosmosMembershipService] Found membership - Id: '{Id}', TeamId: '{TeamId}', PlayerId: '{PlayerId}', Role: '{Role}', LeftAt: {LeftAt}",
                        membership.Id,
                        membership.TeamId,
                        membership.PlayerId,
                        membership.Role,
                        membership.LeftAt
                    );

                    return new UserTeamMembership(
                        authUserId, // Use the auth user ID for the UserTeamMembership
                        membership.TeamId,
                        membership.Role,
                        membership.LeftAt == null
                    );
                }
            }

            _logger.LogInformation(
                "[CosmosMembershipService] No active membership found for player '{PlayerId}' in team '{TeamId}'",
                player.Id,
                teamId
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[CosmosMembershipService] Error retrieving membership for auth user {AuthUserId} and team {TeamId}",
                authUserId,
                teamId
            );
            return null;
        }
    }

    public async Task<List<UserTeamMembership>> GetAllAsync(
        string authUserId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // First, map the auth user ID to player ID
            var player = await _playerService.GetPlayerByAuthUserIdAsync(
                authUserId,
                cancellationToken
            );
            if (player == null)
            {
                _logger.LogWarning("No player found for auth user ID {AuthUserId}", authUserId);
                return new List<UserTeamMembership>();
            }

            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            var memberships = new List<UserTeamMembership>();

            // Query for all active memberships for the player
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.playerId = @playerId AND (c.leftAt = null OR NOT IS_DEFINED(c.leftAt))"
            ).WithParameter("@playerId", player.Id);

            var iterator = container.GetItemQueryIterator<TeamMembership>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);

                foreach (var membership in response)
                {
                    memberships.Add(
                        new UserTeamMembership(
                            authUserId, // Use the auth user ID for the UserTeamMembership
                            membership.TeamId,
                            membership.Role,
                            membership.LeftAt == null
                        )
                    );
                }
            }

            return memberships;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving all memberships for auth user {AuthUserId}",
                authUserId
            );
            return new List<UserTeamMembership>();
        }
    }
}
