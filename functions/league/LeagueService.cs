using Microsoft.Azure.Cosmos;
using SideSpins.Api.Models;
using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SideSpins.Api.Services;

public class LeagueService
{
    private readonly Container _playersContainer;
    private readonly Container _membershipsContainer;
    private readonly Container _matchesContainer;
    private readonly Container _teamsContainer;
    private readonly Container _divisionsContainer;

    public LeagueService(CosmosClient cosmosClient, string databaseName)
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

    public async Task<TeamMembership?> GetMembershipByIdAsync(string membershipId, string teamId)
    {
        try
        {
            var response = await _membershipsContainer.ReadItemAsync<TeamMembership>(membershipId, new PartitionKey(teamId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TeamMembership?> UpdateMembershipAsync(string membershipId, string teamId, TeamMembership membership)
    {
        try
        {
            // Get the existing membership to compare skill levels
            var existingMembership = await GetMembershipByIdAsync(membershipId, teamId);
            
            // Explicitly set the ID and type to ensure consistency
            membership.Id = membershipId;
            membership.Type = "membership";
            membership.TeamId = teamId;
            
            // Ensure we have a valid joinedAt timestamp
            if (membership.JoinedAt == default(DateTime))
            {
                membership.JoinedAt = DateTime.UtcNow;
            }
            
            var response = await _membershipsContainer.ReplaceItemAsync(
                membership, 
                membershipId, 
                new PartitionKey(teamId));
            
            // If skill level changed, update future lineups
            if (existingMembership != null && 
                existingMembership.SkillLevel_9b != membership.SkillLevel_9b && 
                membership.SkillLevel_9b.HasValue)
            {
                await UpdateFutureLineupsForPlayerSkillChangeAsync(
                    membership.PlayerId, 
                    membership.DivisionId, 
                    membership.SkillLevel_9b.Value);
            }
            
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
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

            // Log the match state before update for debugging
            
            // Update only the lineup plan, preserving all other properties
            match.LineupPlan = lineupPlan;
            
            // Ensure all required properties are set correctly - these must be set BEFORE serialization
            if (string.IsNullOrEmpty(match.Id))
            {
                match.Id = id;
            }
            if (string.IsNullOrEmpty(match.DivisionId))
            {
                match.DivisionId = divisionId;
            }
            if (string.IsNullOrEmpty(match.Type))
            {
                match.Type = "teamMatch";
            }
            
            // Ensure we have a valid createdAt timestamp
            if (match.CreatedAt == default(DateTime))
            {
                match.CreatedAt = DateTime.UtcNow;
            }
            
            var response = await _matchesContainer.ReplaceItemAsync(match, id, new PartitionKey(divisionId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"Cosmos exception during lineup update: {ex.StatusCode} - {ex.Message}");
            throw;
        }
    }

    public async Task<TeamMatch> CreateMatchAsync(TeamMatch match)
    {
        // Ensure the ID is properly set
        if (string.IsNullOrEmpty(match.Id))
        {
            match.Id = $"tm_{match.DivisionId}_{match.Week}_{Guid.NewGuid():N}";
        }
        match.CreatedAt = DateTime.UtcNow;
        match.Type = "teamMatch";
        
        var response = await _matchesContainer.CreateItemAsync(match, new PartitionKey(match.DivisionId));
        return response.Resource;
    }

    public async Task<TeamMatch?> UpdateMatchAsync(string id, string divisionId, TeamMatch match)
    {
        try
        {
            // Explicitly set the ID and type to ensure consistency
            match.Id = id;
            match.Type = "teamMatch";
            match.DivisionId = divisionId;
            
            // Ensure we have a valid createdAt timestamp
            if (match.CreatedAt == default(DateTime))
            {
                match.CreatedAt = DateTime.UtcNow;
            }
            
            var response = await _matchesContainer.ReplaceItemAsync(
                match, 
                id, 
                new PartitionKey(divisionId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteMatchAsync(string id, string divisionId)
    {
        try
        {
            await _matchesContainer.DeleteItemAsync<TeamMatch>(id, new PartitionKey(divisionId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IEnumerable<TeamMatch>> GetMatchesByDateRangeAsync(string divisionId, DateTime startDate, DateTime endDate)
    {
        var query = "SELECT * FROM c WHERE c.scheduledAt >= @startDate AND c.scheduledAt <= @endDate ORDER BY c.scheduledAt";
        var queryDefinition = new QueryDefinition(query)
            .WithParameter("@startDate", startDate)
            .WithParameter("@endDate", endDate);
            
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

    /// <summary>
    /// Updates skill levels for a player in all future lineups (today and beyond).
    /// Historic lineups are left untouched.
    /// </summary>
    /// <param name="playerId">The player whose skill level changed</param>
    /// <param name="divisionId">The division the player is in</param>
    /// <param name="newSkillLevel">The new skill level to apply</param>
    private async Task UpdateFutureLineupsForPlayerSkillChangeAsync(string playerId, string divisionId, int newSkillLevel)
    {
        try
        {
            // Get all matches from today forward for this division
            var today = DateTime.UtcNow.Date;
            var futureMatches = await GetMatchesByDateRangeAsync(divisionId, today, DateTime.MaxValue.Date);
            
            bool anyUpdates = false;
            
            foreach (var match in futureMatches)
            {
                bool matchUpdated = false;
                
                // Update home team lineup if player is present
                foreach (var player in match.LineupPlan.Home.Where(p => p.PlayerId == playerId))
                {
                    if (player.SkillLevel != newSkillLevel)
                    {
                        player.SkillLevel = newSkillLevel;
                        matchUpdated = true;
                    }
                }
                
                // Update away team lineup if player is present  
                foreach (var player in match.LineupPlan.Away.Where(p => p.PlayerId == playerId))
                {
                    if (player.SkillLevel != newSkillLevel)
                    {
                        player.SkillLevel = newSkillLevel;
                        matchUpdated = true;
                    }
                }
                
                // If this match was updated, recalculate totals and save
                if (matchUpdated)
                {
                    RecalculateLineupTotals(match.LineupPlan);
                    
                    // Add history entry
                    match.LineupPlan.History.Add(new LineupHistoryEntry
                    {
                        At = DateTime.UtcNow,
                        By = "System",
                        Change = $"Updated skill level for player {playerId} to {newSkillLevel} due to membership change"
                    });
                    
                    // Save the updated match
                    await _matchesContainer.ReplaceItemAsync(match, match.Id, new PartitionKey(match.DivisionId));
                    anyUpdates = true;
                }
            }
            
            if (anyUpdates)
            {
                Console.WriteLine($"Updated skill levels for player {playerId} in future lineups for division {divisionId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating future lineups for player {playerId}: {ex.Message}");
            // Don't throw - we don't want to fail the membership update if lineup updates fail
        }
    }
    
    /// <summary>
    /// Recalculates the skill totals and cap validation for a lineup
    /// </summary>
    /// <param name="lineupPlan">The lineup plan to recalculate</param>
    private void RecalculateLineupTotals(LineupPlan lineupPlan)
    {
        // Calculate home team skill sum (excluding alternates)
        lineupPlan.Totals.HomePlannedSkillSum = lineupPlan.Home
            .Where(p => !p.IsAlternate)
            .Sum(p => p.SkillLevel);
            
        // Calculate away team skill sum (excluding alternates)
        lineupPlan.Totals.AwayPlannedSkillSum = lineupPlan.Away
            .Where(p => !p.IsAlternate)
            .Sum(p => p.SkillLevel);
            
        // Check if teams are within skill cap
        lineupPlan.Totals.HomeWithinCap = lineupPlan.Totals.HomePlannedSkillSum <= lineupPlan.MaxTeamSkillCap;
        lineupPlan.Totals.AwayWithinCap = lineupPlan.Totals.AwayPlannedSkillSum <= lineupPlan.MaxTeamSkillCap;
    }

    /// <summary>
    /// Public method to manually update skill levels for a player in all future lineups.
    /// This can be called directly from an API endpoint for manual corrections.
    /// </summary>
    /// <param name="playerId">The player whose skill level should be updated</param>
    /// <param name="divisionId">The division the player is in</param>
    /// <param name="newSkillLevel">The new skill level to apply</param>
    public async Task UpdateFutureLineupsForPlayerSkillChangePublicAsync(string playerId, string divisionId, int newSkillLevel)
    {
        await UpdateFutureLineupsForPlayerSkillChangeAsync(playerId, divisionId, newSkillLevel);
    }

    // Team operations (partition key: /divisionId)
    public async Task<IEnumerable<Team>> GetTeamsByDivisionIdAsync(string divisionId)
    {
        var query = "SELECT * FROM c";
        var queryDefinition = new QueryDefinition(query);
        var resultSet = _teamsContainer.GetItemQueryIterator<Team>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(divisionId)
            });

        var teams = new List<Team>();
        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            teams.AddRange(response.ToList());
        }

        return teams;
    }

    public async Task<Team?> GetTeamByIdAsync(string id, string divisionId)
    {
        try
        {
            var response = await _teamsContainer.ReadItemAsync<Team>(id, new PartitionKey(divisionId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Team> CreateTeamAsync(Team team)
    {
        // Ensure the ID is properly set
        if (string.IsNullOrEmpty(team.Id))
        {
            // Create a unique team ID based on division and team name
            var safeName = team.Name.ToLower().Replace(" ", "_").Replace("-", "_");
            team.Id = $"team_{safeName}_9b"; 
        }
        team.CreatedAt = DateTime.UtcNow;
        team.Type = "team";
        
        var response = await _teamsContainer.CreateItemAsync(team, new PartitionKey(team.DivisionId));
        return response.Resource;
    }

    public async Task<Team?> UpdateTeamAsync(string id, string divisionId, Team team)
    {
        try
        {
            // Explicitly set the ID and type to ensure consistency
            team.Id = id;
            team.Type = "team";
            team.DivisionId = divisionId;
            
            // Ensure we have a valid createdAt timestamp
            if (team.CreatedAt == default(DateTime))
            {
                team.CreatedAt = DateTime.UtcNow;
            }
            
            var response = await _teamsContainer.ReplaceItemAsync(
                team, 
                id, 
                new PartitionKey(divisionId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteTeamAsync(string id, string divisionId)
    {
        try
        {
            await _teamsContainer.DeleteItemAsync<Team>(id, new PartitionKey(divisionId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
