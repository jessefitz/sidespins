namespace SidesSpins.Functions;

/// <summary>
/// Attribute to require specific team role for function execution
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireTeamRoleAttribute : Attribute
{
    /// <summary>
    /// Minimum role required (player, captain, admin)
    /// </summary>
    public string MinimumRole { get; }

    /// <summary>
    /// Route parameter name containing the team ID (default: "teamId")
    /// </summary>
    public string TeamIdRouteParam { get; }

    public RequireTeamRoleAttribute(string minimumRole, string teamIdRouteParam = "teamId")
    {
        MinimumRole = minimumRole ?? throw new ArgumentNullException(nameof(minimumRole));
        TeamIdRouteParam = teamIdRouteParam ?? "teamId";
    }
}
