using Microsoft.Azure.Cosmos;
using SideSpins.Api.Models;
using System.Net;
using Microsoft.Extensions.Logging;

namespace SideSpins.Api.Services;

public class CosmosService
{
    private readonly Container _playersContainer;
    private readonly Container _membershipsContainer;
    private readonly Container _matchesContainer;
    private readonly Container _teamsContainer;
    private readonly Container _divisionsContainer;

    public CosmosService(CosmosClient cosmosClient, string databaseName)
    {
        var database = cosmosClient.GetDatabase(databaseName);
        
        _playersContainer = database.GetContainer("Players");
        _membershipsContainer = database.GetContainer("TeamMemberships");
        _matchesContainer = database.GetContainer("TeamMatches");
        _teamsContainer = database.GetContainer("Teams");
        _divisionsContainer = database.GetContainer("Divisions");
    }

    // Player operations (partition key: /id - self-partitioned)
    public async Task<IEnumerable<Player>> GetPlayersAsync()
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var resultSet = _playersContainer.GetItemQueryIterator<Player>(queryDefinition);

        var players = new List<Player>();
        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            players.AddRange(response.ToList());
        }

        return players;
    }

    public async Task<Player?> GetPlayerByIdAsync(string id)
    {
        try
        {
            var response = await _playersContainer.ReadItemAsync<Player>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Player> CreatePlayerAsync(Player player)
    {
        // Ensure the ID is properly set
        if (string.IsNullOrEmpty(player.Id))
        {
            player.Id = $"p_{Guid.NewGuid():N}";
        }
        player.CreatedAt = DateTime.UtcNow;
        player.Type = "player";
        
        var response = await _playersContainer.CreateItemAsync(player, new PartitionKey(player.Id));
        return response.Resource;
    }

    public async Task<Player?> UpdatePlayerAsync(string id, Player player)
    {
        try
        {
            // Explicitly set the ID to ensure it matches the route parameter
            player.Id = id;
            player.Type = "player";
            
            // Ensure we have a valid createdAt timestamp
            if (player.CreatedAt == default(DateTime))
            {
                player.CreatedAt = DateTime.UtcNow;
            }
            
            var response = await _playersContainer.ReplaceItemAsync(
                player, 
                id, 
                new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeletePlayerAsync(string id)
    {
        try
        {
            await _playersContainer.DeleteItemAsync<Player>(id, new PartitionKey(id));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // Membership operations (partition key: /teamId)
    public async Task<IEnumerable<TeamMembership>> GetMembershipsByTeamIdAsync(string teamId)
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var resultSet = _membershipsContainer.GetItemQueryIterator<TeamMembership>(
            queryDefinition, 
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(teamId)
            });

        var memberships = new List<TeamMembership>();
        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            memberships.AddRange(response.ToList());
        }

        return memberships;
    }

    public async Task<TeamMembership> CreateMembershipAsync(TeamMembership membership)
    {
        membership.Id = $"m_{membership.TeamId}_{membership.PlayerId}";
        membership.JoinedAt = DateTime.UtcNow;
        membership.Type = "membership";
        var response = await _membershipsContainer.CreateItemAsync(membership, new PartitionKey(membership.TeamId));
        return response.Resource;
    }

    public async Task<bool> DeleteMembershipAsync(string membershipId, string teamId)
    {
        try
        {
            await _membershipsContainer.DeleteItemAsync<TeamMembership>(membershipId, new PartitionKey(teamId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // Match operations (partition key: /divisionId)
    public async Task<IEnumerable<TeamMatch>> GetMatchesByDivisionIdAsync(string divisionId)
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var resultSet = _matchesContainer.GetItemQueryIterator<TeamMatch>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(divisionId) });

        var matches = new List<TeamMatch>();
        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            matches.AddRange(response.ToList());
        }

        return matches;
    }

    public async Task<TeamMatch?> GetMatchByIdAsync(string id, string divisionId)
    {
        try
        {
            var response = await _matchesContainer.ReadItemAsync<TeamMatch>(id, new PartitionKey(divisionId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TeamMatch?> UpdateMatchLineupAsync(string id, string divisionId, LineupPlan lineupPlan)
    {
        try
        {
            var match = await GetMatchByIdAsync(id, divisionId);
            if (match == null) 
                return null;

            match.LineupPlan = lineupPlan;
            var response = await _matchesContainer.ReplaceItemAsync(match, id, new PartitionKey(divisionId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
