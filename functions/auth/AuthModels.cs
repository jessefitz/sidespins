using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace SidesSpins.Functions;

public class AuthSessionRequest
{
    [Required]
    public string SessionJwt { get; set; } = string.Empty;
}

public class AuthSessionResponse
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
    public string? UserId { get; set; }
    public string? TeamId { get; set; }
}

public class AuthResponse
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
    public string? SessionToken { get; set; } // Add token to response
}

public class AuthSmsResponse : AuthResponse
{
    public string? PhoneId { get; set; }
}

public class AuthSmsRequest
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class AuthSmsVerifyRequest
{
    [Required]
    public string PhoneId { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}

public class AuthEmailRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;
}

public class AuthMagicLinkRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

public class StytchUserTrustedMetadata
{
    [JsonProperty("sidespins_role")]
    public string? SidespinsRole { get; set; }
}

public class AppClaims
{
    public string Sub { get; set; } = string.Empty;
    public string? SidespinsRole { get; set; }
    public long Iat { get; set; }
    public long Exp { get; set; }
    public int Ver { get; set; } = 1;
    public string? Jti { get; set; }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SessionToken { get; set; }
    public AppClaims? Claims { get; set; }
    public string? PhoneId { get; set; }
    public string? PhoneNumber { get; set; }
}

// New models for APA-first signup flow
public class SignupInitRequest
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    public string ApaNumber { get; set; } = string.Empty;
}

public class SignupInitResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public string? PhoneId { get; set; }
    public List<UserTeamMembershipInfo>? Memberships { get; set; }
    public UserProfile? Profile { get; set; }
}

public class UserTeamMembershipInfo
{
    public string TeamId { get; set; } = string.Empty;
    public string DivisionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
}

public class UserProfile
{
    public string PlayerId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? SidespinsRole { get; set; }
}

// Lightweight record for middleware use
public sealed record UserTeamMembership(string UserId, string TeamId, string Role, bool Active);

// Enhanced authorization error response
public class AuthorizationErrorResponse
{
    public string Error { get; set; } = "authorization_failed";
    public string Message { get; set; } = string.Empty;
    public string? RequiredRole { get; set; }
    public string? UserRole { get; set; }
    public string? TeamId { get; set; }
    public List<string> AvailableActions { get; set; } = new();
    public string? SuggestedAction { get; set; }
}

public static class AuthorizationErrorMessages
{
    public static AuthorizationErrorResponse CreateInsufficientRole(
        string teamId,
        string requiredRole,
        string? userRole,
        string userId
    )
    {
        return new AuthorizationErrorResponse
        {
            Message = $"You need {requiredRole} permissions to perform this action",
            RequiredRole = requiredRole,
            UserRole = userRole,
            TeamId = teamId,
            AvailableActions = GetAvailableActions(userRole),
            SuggestedAction = GetSuggestedAction(requiredRole, userRole),
        };
    }

    public static AuthorizationErrorResponse CreateNoMembership(string teamId, string userId)
    {
        return new AuthorizationErrorResponse
        {
            Message = "You are not a member of this team",
            TeamId = teamId,
            SuggestedAction = "Contact a team administrator to request access",
        };
    }

    private static List<string> GetAvailableActions(string? userRole)
    {
        return userRole?.ToLower() switch
        {
            "player" => new() { "view_lineup", "view_schedule", "update_availability" },
            "manager" => new()
            {
                "view_lineup",
                "view_schedule",
                "update_availability",
                "manage_lineup",
                "edit_players",
            },
            "admin" => new() { "all_actions" },
            _ => new() { "view_schedule" },
        };
    }

    private static string GetSuggestedAction(string requiredRole, string? userRole)
    {
        return requiredRole?.ToLower() switch
        {
            "manager" when userRole == "player" =>
                "Contact your team manager to request this change",
            "admin" when userRole != "admin" => "Contact a league administrator for assistance",
            _ => "You don't have permission for this action",
        };
    }
}
