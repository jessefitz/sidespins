using System;

namespace SidesSpins.Functions;

/// <summary>
/// Attribute to mark functions that allow API secret authentication as an alternative to JWT tokens.
/// Functions with this attribute can be called using either:
/// 1. JWT token in Authorization header (standard user auth)
/// 2. API secret in x-api-secret header (administrative/system auth)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AllowApiSecretAttribute : Attribute
{
    /// <summary>
    /// Whether to require admin role when using API secret authentication.
    /// Default is true for security - API secret implies admin access.
    /// </summary>
    public bool RequireAdminRole { get; set; } = true;

    /// <summary>
    /// Description of why this endpoint allows API secret access.
    /// Used for documentation and security auditing.
    /// </summary>
    public string Reason { get; set; } = "Administrative operation";

    public AllowApiSecretAttribute() { }

    public AllowApiSecretAttribute(string reason)
    {
        Reason = reason;
    }
}
