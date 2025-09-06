using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using SidesSpins.Functions;

namespace SidesSpins.Functions;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _stytchProjectId;
    private readonly string _stytchSecret;
    private readonly string _jwtSigningKey;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        HttpClient httpClient,
        string stytchProjectId,
        string stytchSecret,
        string jwtSigningKey,
        ILogger<AuthService> logger
    )
    {
        _httpClient = httpClient;
        _stytchProjectId = stytchProjectId;
        _stytchSecret = stytchSecret;
        _jwtSigningKey = jwtSigningKey;
        _logger = logger;

        // Set up HTTP client for Stytch API
        _httpClient.BaseAddress = new Uri("https://test.stytch.com/v1/");
        var authValue = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_stytchProjectId}:{_stytchSecret}")
        );
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            authValue
        );
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
    }

    public async Task<(
        bool Success,
        string? ErrorMessage,
        AppClaims? Claims
    )> ValidateStytchSessionAsync(string sessionJwt)
    {
        try
        {
            // Authenticate the session with Stytch
            var authenticateRequest = new { session_jwt = sessionJwt };

            var requestJson = JsonConvert.SerializeObject(authenticateRequest);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sessions/authenticate", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Stytch session authentication failed. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent
                );
                return (false, "Invalid session", null);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var sessionResponse = JsonConvert.DeserializeObject<StytchSessionResponse>(
                responseContent
            );

            if (sessionResponse?.Session?.UserId == null)
            {
                _logger.LogWarning("Invalid session response from Stytch");
                return (false, "Invalid session response", null);
            }

            // Get user details to read trusted metadata
            var userResponse = await _httpClient.GetAsync(
                $"users/{sessionResponse.Session.UserId}"
            );

            if (!userResponse.IsSuccessStatusCode)
            {
                var errorContent = await userResponse.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Failed to get user details from Stytch. Status: {StatusCode}, Error: {Error}",
                    userResponse.StatusCode,
                    errorContent
                );
                return (false, "Failed to get user details", null);
            }

            var userContent = await userResponse.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<StytchUserResponse>(userContent);

            // Parse trusted metadata isn't serializing properly.
            var trustedMetadata = ParseTrustedMetadata(user?.TrustedMetadata);

            if (trustedMetadata?.Teams?.Team == null)
            {
                _logger.LogWarning(
                    "User {UserId} has no team metadata",
                    sessionResponse.Session.UserId
                );
                return (false, "No team metadata found", null);
            }

            var claims = new AppClaims
            {
                Sub = sessionResponse.Session.UserId,
                TeamId = trustedMetadata.Teams.Team.TeamId,
                TeamRole = trustedMetadata.Teams.Team.TeamRole,
                Iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Exp = DateTimeOffset.UtcNow.AddMinutes(60).ToUnixTimeSeconds(),
            };

            return (true, null, claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Stytch session");
            return (false, "Internal error", null);
        }
    }

    public string GenerateAppJwt(AppClaims claims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSigningKey);

        var claimsList = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, claims.Sub),
            new("team_id", claims.TeamId),
            new("team_role", claims.TeamRole),
            new(JwtRegisteredClaimNames.Iat, claims.Iat.ToString(), ClaimValueTypes.Integer64),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimsList),
            Expires = DateTimeOffset.FromUnixTimeSeconds(claims.Exp).DateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            ),
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public (bool IsValid, AppClaims? Claims) ValidateAppJwt(string jwt)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSigningKey);

            tokenHandler.ValidateToken(
                jwt,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero,
                },
                out SecurityToken validatedToken
            );

            var jwtToken = (JwtSecurityToken)validatedToken;

            var claims = new AppClaims
            {
                Sub = jwtToken.Claims.First(x => x.Type == "sub").Value,
                TeamId = jwtToken.Claims.First(x => x.Type == "team_id").Value,
                TeamRole = jwtToken.Claims.First(x => x.Type == "team_role").Value,
                Iat = long.Parse(jwtToken.Claims.First(x => x.Type == "iat").Value),
                Exp = ((DateTimeOffset)jwtToken.ValidTo).ToUnixTimeSeconds(),
            };

            return (true, claims);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed");
            return (false, null);
        }
    }

    private StytchUserTrustedMetadata? ParseTrustedMetadata(object? trustedMetadata)
    {
        if (trustedMetadata == null)
            return null;

        try
        {
            var json = JsonConvert.SerializeObject(trustedMetadata);
            return JsonConvert.DeserializeObject<StytchUserTrustedMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse trusted metadata");
            return null;
        }
    }

    public async Task<AuthResult> SendSmsCodeAsync(string phoneNumber)
    {
        try
        {
            var requestBody = new { phone_number = phoneNumber };

            var requestJson = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("otps/sms/login_or_create", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var smsResponse = JsonConvert.DeserializeObject<StytchSmsResponse>(responseContent);

                return new AuthResult
                {
                    Success = true,
                    PhoneId = smsResponse?.PhoneId, // Store this for verification
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<StytchErrorResponse>(
                    errorContent
                );
                _logger.LogWarning(
                    "Failed to send SMS code. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent
                );
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = errorResponse?.ErrorMessage ?? "Failed to send SMS code",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS code");
            return new AuthResult { Success = false, ErrorMessage = "Failed to send SMS code" };
        }
    }

    public async Task<AuthResult> VerifySmsCodeAsync(string phoneId, string code)
    {
        try
        {
            var requestBody = new
            {
                method_id = phoneId,
                code = code,
                session_duration_minutes = 60,
            };

            var requestJson = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("otps/authenticate", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var authResponse = JsonConvert.DeserializeObject<StytchAuthResponse>(
                    responseContent
                );

                if (authResponse?.SessionJwt != null)
                {
                    // Validate the session and get claims
                    var (success, errorMessage, claims) = await ValidateStytchSessionAsync(
                        authResponse.SessionJwt
                    );
                    
                    if (success && claims != null)
                    {
                        // Generate App JWT for ongoing session management
                        var appJwt = GenerateAppJwt(claims);
                        
                        return new AuthResult
                        {
                            Success = true,
                            SessionToken = appJwt,
                            Claims = claims,
                        };
                    }
                    
                    return new AuthResult
                    {
                        Success = success,
                        ErrorMessage = errorMessage,
                        Claims = claims,
                    };
                }
                else
                {
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid authentication response",
                    };
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<StytchErrorResponse>(
                    errorContent
                );
                _logger.LogWarning(
                    "SMS verification failed. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent
                );
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = errorResponse?.ErrorMessage ?? "SMS verification failed",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying SMS code");
            return new AuthResult { Success = false, ErrorMessage = "SMS verification failed" };
        }
    }

    public async Task<AuthResult> SendMagicLinkAsync(string email)
    {
        try
        {
            var requestBody = new
            {
                email = email,
                login_magic_link_url = "http://localhost:3000/auth/callback.html", // Update this for production
                signup_magic_link_url = "http://localhost:3000/auth/callback.html", // Update this for production
            };

            var requestJson = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "magic_links/email/login_or_create",
                content
            );

            if (response.IsSuccessStatusCode)
            {
                return new AuthResult { Success = true };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<StytchErrorResponse>(
                    errorContent
                );
                _logger.LogWarning(
                    "Failed to send magic link. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent
                );
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = errorResponse?.ErrorMessage ?? "Failed to send magic link",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending magic link");
            return new AuthResult { Success = false, ErrorMessage = "Failed to send magic link" };
        }
    }

    public async Task<AuthResult> AuthenticateMagicLinkAsync(string token)
    {
        try
        {
            var requestBody = new { token = token, session_duration_minutes = 60 };

            var requestJson = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("magic_links/authenticate", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var authResponse = JsonConvert.DeserializeObject<StytchAuthResponse>(
                    responseContent
                );

                if (authResponse?.SessionJwt != null)
                {
                    // Validate the session and get claims
                    var (success, errorMessage, claims) = await ValidateStytchSessionAsync(
                        authResponse.SessionJwt
                    );
                    
                    if (success && claims != null)
                    {
                        // Generate App JWT for ongoing session management
                        var appJwt = GenerateAppJwt(claims);
                        
                        return new AuthResult
                        {
                            Success = true,
                            SessionToken = appJwt,
                            Claims = claims,
                        };
                    }
                    
                    return new AuthResult
                    {
                        Success = success,
                        ErrorMessage = errorMessage,
                        Claims = claims,
                    };
                }
                else
                {
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid authentication response",
                    };
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<StytchErrorResponse>(
                    errorContent
                );
                _logger.LogWarning(
                    "Magic link authentication failed. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode,
                    errorContent
                );
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage =
                        errorResponse?.ErrorMessage ?? "Magic link authentication failed",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating magic link");
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Magic link authentication failed",
            };
        }
    }
}

// Stytch API response models
public class StytchSessionResponse
{
    public StytchSession? Session { get; set; }
}

public class StytchSession
{
    [JsonProperty("user_id")]
    public string? UserId { get; set; }
}

public class StytchUserResponse
{
    [JsonProperty("user_id")]
    public string? UserId { get; set; }
    
    [JsonProperty("external_id")]
    public string? ExternalId { get; set; }
    
    [JsonProperty("trusted_metadata")]
    public object? TrustedMetadata { get; set; }
    
    [JsonProperty("untrusted_metadata")]
    public object? UntrustedMetadata { get; set; }
    
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    [JsonProperty("name")]
    public StytchUserName? Name { get; set; }
    
    [JsonProperty("phone_numbers")]
    public StytchPhoneNumber[]? PhoneNumbers { get; set; }
    
    [JsonProperty("emails")]
    public object[]? Emails { get; set; }
    
    [JsonProperty("roles")]
    public string[]? Roles { get; set; }
}

public class StytchUserName
{
    [JsonProperty("first_name")]
    public string? FirstName { get; set; }
    
    [JsonProperty("last_name")]
    public string? LastName { get; set; }
    
    [JsonProperty("middle_name")]
    public string? MiddleName { get; set; }
}

public class StytchPhoneNumber
{
    [JsonProperty("phone_id")]
    public string? PhoneId { get; set; }
    
    [JsonProperty("phone_number")]
    public string? PhoneNumber { get; set; }
    
    [JsonProperty("verified")]
    public bool Verified { get; set; }
}

public class StytchAuthResponse
{
    [JsonProperty("session_jwt")]
    public string? SessionJwt { get; set; }

    [JsonProperty("status_code")]
    public int StatusCode { get; set; }
}

public class StytchErrorResponse
{
    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("status_code")]
    public int StatusCode { get; set; }
}

public class StytchSmsResponse
{
    [JsonProperty("phone_id")]
    public string? PhoneId { get; set; }

    [JsonProperty("user_id")]
    public string? UserId { get; set; }

    [JsonProperty("status_code")]
    public int StatusCode { get; set; }
}
