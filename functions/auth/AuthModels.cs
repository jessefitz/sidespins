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
    public Teams? Teams { get; set; }
}

public class Teams
{
    public Team? Team { get; set; }
}

public class Team
{
    [JsonProperty("team_id")]
    public string TeamId { get; set; } = string.Empty;
    
    [JsonProperty("team_role")]
    public string TeamRole { get; set; } = string.Empty;
}

public class AppClaims
{
    public string Sub { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string TeamRole { get; set; } = string.Empty;
    public long Iat { get; set; }
    public long Exp { get; set; }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SessionToken { get; set; }
    public AppClaims? Claims { get; set; }
    public string? PhoneId { get; set; }
}
