using SidesSpins.Functions;

namespace SidesSpins.Functions;

/// <summary>
/// Service for querying team memberships for authorization purposes
/// </summary>
public interface IMembershipService
{
    /// <summary>
    /// Get active team membership for a user
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="teamId">The team ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User team membership if found and active, null otherwise</returns>
    Task<UserTeamMembership?> GetAsync(
        string userId,
        string teamId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get all active team memberships for a user
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active team memberships</returns>
    Task<List<UserTeamMembership>> GetAllAsync(
        string userId,
        CancellationToken cancellationToken = default
    );
}
