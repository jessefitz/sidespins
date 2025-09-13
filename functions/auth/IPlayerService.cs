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
    Task<Player?> GetPlayerByAuthUserIdAsync(
        string authUserId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a player by their ID
    /// </summary>
    /// <param name="playerId">The player's ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player if found, null otherwise</returns>
    Task<Player?> GetPlayerByIdAsync(
        string playerId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get players by their phone number
    /// </summary>
    /// <param name="phoneNumber">The phone number to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of players with the given phone number</returns>
    Task<List<Player>> GetPlayersByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update a player document
    /// </summary>
    /// <param name="player">The player to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated player</returns>
    Task<Player> UpdatePlayerAsync(Player player, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a player by their APA number
    /// </summary>
    /// <param name="apaNumber">The APA number to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player if found, null otherwise</returns>
    Task<Player?> GetPlayerByApaNumberAsync(
        string apaNumber,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Check if a player with the given APA number already has an auth user ID
    /// </summary>
    /// <param name="apaNumber">The APA number to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a player with this APA number already has an authUserId, false otherwise</returns>
    Task<bool> IsApaNumberAlreadyRegisteredAsync(
        string apaNumber,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Check if a player with the given phone number already has an auth user ID
    /// </summary>
    /// <param name="phoneNumber">The phone number to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a player with this phone number already has an authUserId, false otherwise</returns>
    Task<bool> IsPhoneNumberAlreadyRegisteredAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default
    );
}
