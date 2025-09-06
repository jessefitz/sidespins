using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SidesSpins.Functions;

public class AuthFunctions
{
    private readonly ILogger<AuthFunctions> _logger;
    private readonly AuthService _authService;

    public AuthFunctions(ILogger<AuthFunctions> logger, AuthService authService)
    {
        _logger = logger;
        _authService = authService;
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
                    new AuthSessionResponse
                    {
                        Ok = false,
                        Message = "Authentication failed",
                    }
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
                    TeamId = claims.TeamId,
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
                return new UnauthorizedObjectResult(
                    new { message = "Authentication failed" }
                );
            }

            return new OkObjectResult(
                new
                {
                    userId = claims.Sub,
                    teamId = claims.TeamId,
                    teamRole = claims.TeamRole,
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

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddHours(24),
                };

                // Set the session cookie
                req.HttpContext.Response.Cookies.Append("ssid", result.SessionToken, cookieOptions);

                // Also set team cookie if available
                if (!string.IsNullOrEmpty(result.Claims?.TeamId))
                {
                    req.HttpContext.Response.Cookies.Append("team", result.Claims.TeamId);
                }

                return new OkObjectResult(
                    new AuthResponse { Ok = true, Message = "Authentication successful" }
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

                // Also set team cookie if available
                if (!string.IsNullOrEmpty(result.Claims?.TeamId))
                {
                    req.HttpContext.Response.Cookies.Append("team", result.Claims.TeamId);
                }

                return new OkObjectResult(
                    new AuthResponse { Ok = true, Message = "Authentication successful" }
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
}
