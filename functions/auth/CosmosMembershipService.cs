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
    private readonly ILogger<CosmosMembershipService> _logger;
    private readonly string _databaseName;
    private readonly string _containerName;

    public CosmosMembershipService(
        CosmosClient cosmosClient, 
        ILogger<CosmosMembershipService> logger,
        string databaseName = "sidespins",
        string containerName = "league"
    )
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        _databaseName = databaseName;
        _containerName = containerName;
    }

    public async Task<UserTeamMembership?> GetAsync(string userId, string teamId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            
            // Query for active membership for the specific user and team
            // Remove type filter since TeamMemberships container only contains memberships
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.playerId = @playerId AND c.teamId = @teamId AND (c.leftAt = null OR NOT IS_DEFINED(c.leftAt))"
            )
            .WithParameter("@playerId", userId)
            .WithParameter("@teamId", teamId);

            var iterator = container.GetItemQueryIterator<TeamMembership>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var membership = response.FirstOrDefault();
                
                if (membership != null)
                {
                    return new UserTeamMembership(
                        membership.PlayerId,
                        membership.TeamId,
                        membership.Role,
                        membership.LeftAt == null
                    );
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving membership for user {UserId} and team {TeamId}", userId, teamId);
            return null;
        }
    }

    public async Task<List<UserTeamMembership>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, _containerName);
            var memberships = new List<UserTeamMembership>();
            
            // Query for all active memberships for the user
            // Remove type filter since TeamMemberships container only contains memberships
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.playerId = @playerId AND (c.leftAt = null OR NOT IS_DEFINED(c.leftAt))"
            )
            .WithParameter("@playerId", userId);

            var iterator = container.GetItemQueryIterator<TeamMembership>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                
                foreach (var membership in response)
                {
                    memberships.Add(new UserTeamMembership(
                        membership.PlayerId,
                        membership.TeamId,
                        membership.Role,
                        membership.LeftAt == null
                    ));
                }
            }

            return memberships;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all memberships for user {UserId}", userId);
            return new List<UserTeamMembership>();
        }
    }
}
