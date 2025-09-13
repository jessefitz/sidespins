using System;

namespace SidesSpins.Functions;

[AttributeUsage(AttributeTargets.Method)]
public class RequireAuthenticationAttribute : Attribute
{
    public string RequiredRole { get; set; }
    public bool RequireTeamAccess { get; set; } = true;

    public RequireAuthenticationAttribute(string requiredRole = "player")
    {
        RequiredRole = requiredRole;
    }
}
