using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Models;
using SideSpins.Api.Services;

namespace SidesSpins.Functions;

public class AuthFunctions
{
    private readonly ILogger<AuthFunctions> _logger;
    private readonly AuthService _authService;
    private readonly LeagueService _leagueService;
    private readonly IMembershipService _membershipService;
    private readonly IPlayerService _playerService;

    public AuthFunctions(
        ILogger<AuthFunctions> logger,
        AuthService authService,
        LeagueService leagueService,
        IMembershipService membershipService,
        IPlayerService playerService
    )
    {
        _logger = logger;
        _authService = authService;
        _leagueService = leagueService;
        _membershipService = membershipService;
        _playerService = playerService;
    }

    [Function("ValidateSession")]
    public async Task<IActionResult> ValidateSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/session")] HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var sessionRequest = JsonSerializer.Deserialize<AuthSessionRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (sessionRequest == null || string.IsNullOrEmpty(sessionRequest.SessionJwt))
            {
                return new BadRequestObjectResult(
                    new AuthSessionResponse { Ok = false, Message = "Session JWT is required" }
                );
            }

            var (isValid, claims) = _authService.ValidateAppJwt(sessionRequest.SessionJwt);

            if (!isValid || claims == null)
            {
                return new UnauthorizedObjectResult(
                    new AuthSessionResponse { Ok = false, Message = "Authentication failed" }
                );
            }

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddHours(24),
            };

            // Set the session cookie
            req.HttpContext.Response.Cookies.Append(
                "ssid",
                sessionRequest.SessionJwt,
                cookieOptions
            );

            return new OkObjectResult(
                new AuthSessionResponse
                {
                    Ok = true,
                    Message = "Authentication successful",
                    UserId = claims.Sub,
                    TeamId = null, // Team context will be handled separately
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session");
            return new StatusCodeResult(500);
        }
    }

    [Function("Logout")]
    public IActionResult Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/logout")] HttpRequest req
    )
    {
        try
        {
            // Clear the session cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(-1),
            };

            req.HttpContext.Response.Cookies.Append("ssid", "", cookieOptions);

            return new OkObjectResult(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetCurrentUser")]
    public IActionResult GetCurrentUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/user")] HttpRequest req
    )
    {
        try
        {
            if (
                !req.Cookies.TryGetValue("ssid", out var sessionJwt)
                || string.IsNullOrEmpty(sessionJwt)
            )
            {
                return new UnauthorizedObjectResult(new { message = "Not authenticated" });
            }

            var (isValid, claims) = _authService.ValidateAppJwt(sessionJwt);

            if (!isValid || claims == null)
            {
                return new UnauthorizedObjectResult(new { message = "Authentication failed" });
            }

            return new OkObjectResult(
                new
                {
                    userId = claims.Sub,
                    playerId = claims.PlayerId,
                    sidespinsRole = claims.SidespinsRole,
                    authenticated = true,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return new StatusCodeResult(500);
        }
    }

    [Function("SendSmsCode")]
    public async Task<IActionResult> SendSmsCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/sms/send")] HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var smsRequest = JsonSerializer.Deserialize<AuthSmsRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (smsRequest == null || string.IsNullOrEmpty(smsRequest.PhoneNumber))
            {
                return new BadRequestObjectResult(
                    new AuthResponse { Ok = false, Message = "Phone number is required" }
                );
            }

            var result = await _authService.SendSmsCodeAsync(smsRequest.PhoneNumber);

            if (result.Success)
            {
                return new OkObjectResult(
                    new AuthSmsResponse
                    {
                        Ok = true,
                        Message = "SMS code sent successfully",
                        PhoneId = result.PhoneId,
                    }
                );
            }
            else
            {
                return new BadRequestObjectResult(
                    new AuthResponse
                    {
                        Ok = false,
                        Message = result.ErrorMessage ?? "Failed to send SMS code",
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS code");
            return new StatusCodeResult(500);
        }
    }

    [Function("SendSmsCodeForLogin")]
    public async Task<IActionResult> SendSmsCodeForLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/sms/send-login")]
            HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var smsRequest = JsonSerializer.Deserialize<AuthSmsRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (smsRequest == null || string.IsNullOrEmpty(smsRequest.PhoneNumber))
            {
                return new BadRequestObjectResult(
                    new AuthResponse { Ok = false, Message = "Phone number is required" }
                );
            }

            var result = await _authService.SendSmsCodeForLoginAsync(smsRequest.PhoneNumber);

            if (result.Success)
            {
                return new OkObjectResult(
                    new AuthSmsResponse
                    {
                        Ok = true,
                        Message = "SMS code sent successfully",
                        PhoneId = result.PhoneId,
                    }
                );
            }
            else
            {
                return new BadRequestObjectResult(
                    new AuthResponse
                    {
                        Ok = false,
                        Message =
                            result.ErrorMessage
                            ?? "Failed to send SMS code. Please make sure you have an existing account.",
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS code for login");
            return new StatusCodeResult(500);
        }
    }

    [Function("VerifySmsCode")]
    public async Task<IActionResult> VerifySmsCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/sms/verify")]
            HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var verifyRequest = JsonSerializer.Deserialize<AuthSmsVerifyRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (
                verifyRequest == null
                || string.IsNullOrEmpty(verifyRequest.PhoneId)
                || string.IsNullOrEmpty(verifyRequest.Code)
            )
            {
                return new BadRequestObjectResult(
                    new AuthResponse { Ok = false, Message = "Phone ID and code are required" }
                );
            }

            var result = await _authService.VerifySmsCodeAsync(
                verifyRequest.PhoneId,
                verifyRequest.Code
            );

            if (result.Success)
            {
                if (string.IsNullOrEmpty(result.SessionToken))
                {
                    return new StatusCodeResult(500);
                }

                // IMPORTANT: Link authUserId to player during first successful authentication
                if (
                    !string.IsNullOrEmpty(result.Claims?.Sub)
                    && !string.IsNullOrEmpty(result.PhoneNumber)
                )
                {
                    await LinkAuthUserIdToPlayerAsync(result.Claims.Sub, result.PhoneNumber);

                    // CRITICAL FIX: Regenerate JWT after player linking to include player_id claim
                    var updatedResult = await _authService.RegenerateJwtWithPlayerClaimsAsync(
                        result.Claims.Sub
                    );
                    if (updatedResult != null && updatedResult.Success)
                    {
                        _logger.LogInformation(
                            "Successfully regenerated JWT with player claims for authUserId: {AuthUserId}",
                            result.Claims.Sub
                        );
                        result = updatedResult; // Use the updated result with player_id claim
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to regenerate JWT with player claims for authUserId: {AuthUserId}, using original JWT",
                            result.Claims.Sub
                        );
                        // Continue with original JWT - better than failing completely
                    }
                }

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddHours(24),
                };

                // Set the session cookie
                req.HttpContext.Response.Cookies.Append("ssid", result.SessionToken, cookieOptions);

                return new OkObjectResult(
                    new AuthResponse
                    {
                        Ok = true,
                        Message = "Authentication successful",
                        SessionToken = result.SessionToken,
                    }
                );
            }
            else
            {
                return new BadRequestObjectResult(
                    new AuthResponse
                    {
                        Ok = false,
                        Message = result.ErrorMessage ?? "SMS verification failed",
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying SMS code");
            return new StatusCodeResult(500);
        }
    }

    [Function("SendMagicLink")]
    public async Task<IActionResult> SendMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/email/send")]
            HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var emailRequest = JsonSerializer.Deserialize<AuthEmailRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (emailRequest == null || string.IsNullOrEmpty(emailRequest.Email))
            {
                return new BadRequestObjectResult(
                    new AuthResponse { Ok = false, Message = "Email is required" }
                );
            }

            var result = await _authService.SendMagicLinkAsync(emailRequest.Email);

            if (result.Success)
            {
                return new OkObjectResult(
                    new AuthResponse { Ok = true, Message = "Magic link sent successfully" }
                );
            }
            else
            {
                return new BadRequestObjectResult(
                    new AuthResponse
                    {
                        Ok = false,
                        Message = result.ErrorMessage ?? "Failed to send magic link",
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending magic link");
            return new StatusCodeResult(500);
        }
    }

    [Function("AuthenticateMagicLink")]
    public async Task<IActionResult> AuthenticateMagicLink(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/email/authenticate")]
            HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var linkRequest = JsonSerializer.Deserialize<AuthMagicLinkRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (linkRequest == null || string.IsNullOrEmpty(linkRequest.Token))
            {
                return new BadRequestObjectResult(
                    new AuthResponse { Ok = false, Message = "Token is required" }
                );
            }

            var result = await _authService.AuthenticateMagicLinkAsync(linkRequest.Token);

            if (result.Success)
            {
                if (string.IsNullOrEmpty(result.SessionToken))
                {
                    return new StatusCodeResult(500);
                }

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddHours(24),
                };

                // Set the session cookie
                req.HttpContext.Response.Cookies.Append("ssid", result.SessionToken, cookieOptions);

                return new OkObjectResult(
                    new AuthResponse
                    {
                        Ok = true,
                        Message = "Authentication successful",
                        SessionToken = result.SessionToken,
                    }
                );
            }
            else
            {
                return new BadRequestObjectResult(
                    new AuthResponse
                    {
                        Ok = false,
                        Message = result.ErrorMessage ?? "Magic link authentication failed",
                    }
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating magic link");
            return new StatusCodeResult(500);
        }
    }

    [Function("SignupInit")]
    public async Task<IActionResult> SignupInit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signup/init")]
            HttpRequest req
    )
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var signupRequest = JsonSerializer.Deserialize<SignupInitRequest>(
                body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );

            if (
                signupRequest == null
                || string.IsNullOrEmpty(signupRequest.PhoneNumber)
                || string.IsNullOrEmpty(signupRequest.ApaNumber)
            )
            {
                return new BadRequestObjectResult(
                    new SignupInitResponse
                    {
                        Success = false,
                        Message = "Phone number and APA member number are required",
                    }
                );
            }

            // Validate phone number format (basic E.164 check)
            if (!signupRequest.PhoneNumber.StartsWith("+") || signupRequest.PhoneNumber.Length < 10)
            {
                return new BadRequestObjectResult(
                    new SignupInitResponse
                    {
                        Success = false,
                        Message = "Phone number must be in E.164 format (e.g., +1234567890)",
                    }
                );
            }

            // Step 1: Validate APA number exists in Players collection
            var players = await _leagueService.GetPlayersAsync();
            var player = players.FirstOrDefault(p =>
                string.Equals(
                    p.ApaNumber,
                    signupRequest.ApaNumber,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (player == null)
            {
                _logger.LogWarning(
                    "Signup attempt with invalid APA number: {ApaNumber}",
                    signupRequest.ApaNumber
                );
                return new ConflictObjectResult(
                    new SignupInitResponse
                    {
                        Success = false,
                        Message =
                            "We couldn't find that APA member number. Contact your captain if you believe this is an error.",
                    }
                );
            }

            // Step 2: Check if this APA number is already registered to prevent duplicate accounts
            var isApaAlreadyRegistered = await _playerService.IsApaNumberAlreadyRegisteredAsync(
                signupRequest.ApaNumber
            );
            if (isApaAlreadyRegistered)
            {
                _logger.LogWarning(
                    "Signup attempt with already registered APA number: {ApaNumber}",
                    signupRequest.ApaNumber
                );
                return new ConflictObjectResult(
                    new SignupInitResponse
                    {
                        Success = false,
                        Message =
                            "This APA member number has already been registered. If you believe this is an error, please contact support.",
                    }
                );
            }

            // Step 3: Check if this phone number is already registered to prevent duplicate accounts
            var isPhoneAlreadyRegistered = await _playerService.IsPhoneNumberAlreadyRegisteredAsync(
                signupRequest.PhoneNumber
            );
            if (isPhoneAlreadyRegistered)
            {
                _logger.LogWarning(
                    "Signup attempt with already registered phone number: {PhoneNumber}",
                    signupRequest.PhoneNumber
                );
                return new ConflictObjectResult(
                    new SignupInitResponse
                    {
                        Success = false,
                        Message =
                            "This phone number has already been registered. If you believe this is an error, please contact support.",
                    }
                );
            }

            // Step 4: Reconcile phone number (update if missing or different)
            if (
                string.IsNullOrEmpty(player.PhoneNumber)
                || !string.Equals(
                    player.PhoneNumber,
                    signupRequest.PhoneNumber,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                player.PhoneNumber = signupRequest.PhoneNumber;
                await _leagueService.UpdatePlayerAsync(player.Id, player);
                _logger.LogInformation("Updated phone number for player {PlayerId}", player.Id);
            }

            // Step 5: Resolve active memberships for initial UI state
            var memberships = await _membershipService.GetAllAsync(player.Id);
            var membershipInfos = new List<UserTeamMembershipInfo>();

            foreach (var membership in memberships)
            {
                // Get team name for UI display (we already have divisionId from membership)
                var team = await _leagueService.GetTeamByIdAsync(membership.TeamId, ""); // Try without division first

                membershipInfos.Add(
                    new UserTeamMembershipInfo
                    {
                        TeamId = membership.TeamId,
                        DivisionId = "", // We'll populate this later when needed
                        Role = membership.Role,
                        TeamName = team?.Name ?? membership.TeamId, // Fallback to teamId if name not found
                    }
                );
            }

            // Step 6: Proceed to Stytch SMS OTP
            var smsResult = await _authService.SendSmsCodeForSignupAsync(signupRequest.PhoneNumber);

            if (!smsResult.Success)
            {
                _logger.LogError("Failed to send SMS for signup: {Error}", smsResult.ErrorMessage);
                return new BadRequestObjectResult(
                    new SignupInitResponse
                    {
                        Success = false,
                        Message = smsResult.ErrorMessage ?? "Failed to send verification code",
                    }
                );
            }

            // Step 7: Return success with phone ID for verification step
            // Include player ID for later authUserId linking
            return new OkObjectResult(
                new SignupInitResponse
                {
                    Success = true,
                    Message = "Verification code sent successfully",
                    PhoneId = smsResult.PhoneId, // Include phone ID for verification
                    Profile = new UserProfile
                    {
                        PlayerId = player.Id,
                        FirstName = player.FirstName,
                        LastName = player.LastName,
                        SidespinsRole = "member", // Default role for new signups
                    },
                    Memberships = membershipInfos,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during signup initialization");
            return new StatusCodeResult(500);
        }
    }

    private Task<string> GetDivisionIdForTeam(string teamId)
    {
        try
        {
            // Since we don't have a direct way to get team without division ID,
            // we'll need to query all divisions or use a different approach
            // For now, return empty string - this can be optimized later
            return Task.FromResult("");
        }
        catch
        {
            return Task.FromResult("");
        }
    }

    /// <summary>
    /// Links an auth user ID to a player based on phone number lookup
    /// This is called during the first successful SMS verification
    /// </summary>
    private async Task LinkAuthUserIdToPlayerAsync(string authUserId, string phoneNumber)
    {
        try
        {
            _logger.LogInformation(
                "Attempting to link authUserId {AuthUserId} with phone {Phone}",
                authUserId,
                phoneNumber
            );

            // Get players by phone number using the PlayerService
            var players = await _playerService.GetPlayersByPhoneNumberAsync(phoneNumber);
            _logger.LogInformation(
                "Found {Count} players with phone number {Phone}",
                players.Count,
                phoneNumber
            );

            if (players.Count == 0)
            {
                _logger.LogWarning(
                    "No players found with phone number {Phone} for authUserId {AuthUserId}",
                    phoneNumber,
                    authUserId
                );
                return;
            }

            if (players.Count > 1)
            {
                _logger.LogWarning(
                    "Multiple players found with phone number {Phone}. Cannot auto-link authUserId {AuthUserId}",
                    phoneNumber,
                    authUserId
                );

                // Log all candidates for manual review
                foreach (var candidate in players)
                {
                    _logger.LogInformation(
                        "Candidate player: {PlayerId} - {FirstName} {LastName} (APA: {ApaNumber}, AuthUserId: {AuthUserId})",
                        candidate.Id,
                        candidate.FirstName,
                        candidate.LastName,
                        candidate.ApaNumber,
                        candidate.AuthUserId
                    );
                }
                return;
            }

            // Exactly one player found with this phone number
            var player = players.First();

            // Check if this player already has an authUserId
            if (!string.IsNullOrEmpty(player.AuthUserId))
            {
                if (player.AuthUserId == authUserId)
                {
                    _logger.LogInformation(
                        "Player {PlayerId} already linked to authUserId {AuthUserId}",
                        player.Id,
                        authUserId
                    );
                    return;
                }
                else
                {
                    _logger.LogError(
                        "DUPLICATE ACCOUNT ATTEMPT: Player {PlayerId} (APA: {ApaNumber}) already linked to different authUserId {ExistingAuthUserId}. Attempted to link to {NewAuthUserId}",
                        player.Id,
                        player.ApaNumber,
                        player.AuthUserId,
                        authUserId
                    );
                    // This should not happen due to our pre-validation, but log it as an error if it does
                    return;
                }
            }

            // Check if this authUserId is already linked to another player
            var existingPlayer = await _playerService.GetPlayerByAuthUserIdAsync(authUserId);
            if (existingPlayer != null)
            {
                _logger.LogError(
                    "DUPLICATE ACCOUNT ATTEMPT: AuthUserId {AuthUserId} already linked to player {ExistingPlayerId} (APA: {ExistingApaNumber}). Attempted to link to player {PlayerId} (APA: {ApaNumber})",
                    authUserId,
                    existingPlayer.Id,
                    existingPlayer.ApaNumber,
                    player.Id,
                    player.ApaNumber
                );
                // This should not happen due to our pre-validation, but log it as an error if it does
                return;
            }

            // Safe to link - update the player with authUserId
            player.AuthUserId = authUserId;
            await _playerService.UpdatePlayerAsync(player);

            _logger.LogInformation(
                "Successfully linked authUserId {AuthUserId} to player {PlayerId} ({FirstName} {LastName}, APA: {ApaNumber})",
                authUserId,
                player.Id,
                player.FirstName,
                player.LastName,
                player.ApaNumber
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error linking authUserId {AuthUserId} to player with phone {Phone}",
                authUserId,
                phoneNumber
            );
            // Don't throw - this shouldn't break the authentication flow
        }
    }
}
