using SideSpins.Api.Models;

namespace SidesSpins.Functions;

/// <summary>
/// Service for player-related operations
/// </summary>
public interface IPlayerService
{
    /// <summary>
    /// Get a player by their authentication user ID
    /// </summary>
    /// <param name="authUserId">The authentication provider's user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player if found, null otherwise</returns>
    Task<Player?> GetPlayerByAuthUserIdAsync(string authUserId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a player by their ID
    /// </summary>
    /// <param name="playerId">The player's ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player if found, null otherwise</returns>
    Task<Player?> GetPlayerByIdAsync(string playerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get players by their phone number
    /// </summary>
    /// <param name="phoneNumber">The phone number to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of players with the given phone number</returns>
    Task<List<Player>> GetPlayersByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a player document
    /// </summary>
    /// <param name="player">The player to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated player</returns>
    Task<Player> UpdatePlayerAsync(Player player, CancellationToken cancellationToken = default);
}
