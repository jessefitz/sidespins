using Microsoft.Azure.Functions.Worker;

namespace SidesSpins.Functions;

public static class FunctionContextExtensions
{
    public static AppClaims? GetUserClaims(this FunctionContext context)
    {
        return context.Items.TryGetValue("UserClaims", out var claims) ? (AppClaims)claims : null;
    }

    public static string? GetUserId(this FunctionContext context)
    {
        return context.Items.TryGetValue("UserId", out var userId) ? userId.ToString() : null;
    }

    public static string? GetTeamId(this FunctionContext context)
    {
        return context.Items.TryGetValue("TeamId", out var teamId) ? teamId.ToString() : null;
    }

    public static string? GetTeamRole(this FunctionContext context)
    {
        return context.Items.TryGetValue("TeamRole", out var role) ? role.ToString() : null;
    }

    public static bool IsUserInTeam(this FunctionContext context, string teamId)
    {
        var userTeamId = context.GetTeamId();
        return !string.IsNullOrEmpty(userTeamId)
            && userTeamId.Equals(teamId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasMinimumRole(this FunctionContext context, string minimumRole)
    {
        var userRole = context.GetTeamRole();
        if (string.IsNullOrEmpty(userRole))
            return false;

        var roleHierarchy = new Dictionary<string, int>
        {
            { "player", 1 },
            { "manager", 2 },
            { "admin", 3 },
        };

        return roleHierarchy.GetValueOrDefault(userRole, 0)
            >= roleHierarchy.GetValueOrDefault(minimumRole, 0);
    }
}
