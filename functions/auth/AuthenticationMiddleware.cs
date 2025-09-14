using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace SidesSpins.Functions;

public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly AuthService _authService;
    private readonly IMembershipService _membershipService;

    public AuthenticationMiddleware(
        ILogger<AuthenticationMiddleware> logger,
        AuthService authService,
        IMembershipService membershipService
    )
    {
        _logger = logger;
        _authService = authService;
        _membershipService = membershipService;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            await next(context);
            return;
        }

        // Discover attributes on the function method
        var requiresAuth = HasAuthenticationAttribute(context);
        var teamRoleRequirement = GetTeamRoleRequirement(context);

        if (!requiresAuth && teamRoleRequirement == null)
        {
            await next(context);
            return;
        }

        try
        {
            // Extract and validate JWT
            var jwt = ExtractJwtFromRequest(httpContext.Request);
            if (string.IsNullOrEmpty(jwt))
            {
                await WriteUnauthorizedResponse(httpContext.Response);
                return;
            }

            var (isValid, claims) = _authService.ValidateAppJwt(jwt);
            if (!isValid || claims == null)
            {
                await WriteUnauthorizedResponse(httpContext.Response);
                return;
            }

            // Store identity in context
            context.Items["UserId"] = claims.Sub;
            context.Items["UserClaims"] = claims; // Store the full claims object
            context.Items["SidespinsRole"] = claims.SidespinsRole ?? string.Empty;

            // If team role is required, validate team membership
            if (teamRoleRequirement != null)
            {
                var teamId = ExtractTeamIdFromRoute(
                    httpContext.Request,
                    teamRoleRequirement.TeamIdRouteParam
                );
                if (string.IsNullOrEmpty(teamId))
                {
                    _logger.LogWarning("Team ID not found in route for team role requirement");
                    await WriteForbiddenResponse(httpContext.Response);
                    return;
                }

                // Check for global admin bypass
                if (IsGlobalAdmin(claims.SidespinsRole))
                {
                    // Global admin has access to all teams
                    context.Items["ActiveMembership"] = new UserTeamMembership(
                        claims.Sub,
                        teamId,
                        "admin",
                        true
                    );
                }
                else
                {
                    // Query membership for the specific team
                    var membership = await _membershipService.GetAsync(claims.Sub, teamId);
                    if (membership == null || !membership.Active)
                    {
                        _logger.LogWarning(
                            "User {UserId} has no active membership for team {TeamId}",
                            claims.Sub,
                            teamId
                        );
                        await WriteForbiddenResponse(
                            httpContext.Response,
                            AuthorizationErrorMessages.CreateNoMembership(teamId, claims.Sub)
                        );
                        return;
                    }

                    // Validate role hierarchy
                    if (!IsAtLeast(membership.Role, teamRoleRequirement.MinimumRole))
                    {
                        _logger.LogWarning(
                            "User {UserId} has insufficient role {UserRole} for team {TeamId}, required: {RequiredRole}",
                            claims.Sub,
                            membership.Role,
                            teamId,
                            teamRoleRequirement.MinimumRole
                        );
                        await WriteForbiddenResponse(
                            httpContext.Response,
                            AuthorizationErrorMessages.CreateInsufficientRole(
                                teamId,
                                teamRoleRequirement.MinimumRole,
                                membership.Role,
                                claims.Sub
                            )
                        );
                        return;
                    }

                    context.Items["ActiveMembership"] = membership;
                }

                context.Items["TeamId"] = teamId;
            }

            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication middleware error");
            await WriteUnauthorizedResponse(httpContext.Response);
        }
    }

    private string? ExtractJwtFromRequest(HttpRequest request)
    {
        // Check Authorization header
        if (request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var token = authHeader.FirstOrDefault()?.Replace("Bearer ", "");
            if (!string.IsNullOrEmpty(token))
                return token;
        }

        // Check cookies - first try "ssid" (session ID), then fallback to "auth_token"
        if (request.Cookies.TryGetValue("ssid", out var sessionToken))
        {
            return sessionToken;
        }

        if (request.Cookies.TryGetValue("auth_token", out var cookieToken))
        {
            return cookieToken;
        }

        return null;
    }

    private bool HasAuthenticationAttribute(FunctionContext context)
    {
        try
        {
            // Get the method info from the function definition
            var functionName = context.FunctionDefinition.Name;

            // Get all types in the current assembly
            var assembly = Assembly.GetExecutingAssembly();
            var functionTypes = assembly
                .GetTypes()
                .Where(t =>
                    t.GetMethods()
                        .Any(m => m.GetCustomAttribute<FunctionAttribute>()?.Name == functionName)
                );

            foreach (var type in functionTypes)
            {
                var method = type.GetMethods()
                    .FirstOrDefault(m =>
                        m.GetCustomAttribute<FunctionAttribute>()?.Name == functionName
                    );

                if (method != null)
                {
                    var authAttribute = method.GetCustomAttribute<RequireAuthenticationAttribute>();
                    if (authAttribute != null)
                    {
                        context.Items["RequiredRole"] = authAttribute.RequiredRole;
                        context.Items["RequireTeamAccess"] = authAttribute.RequireTeamAccess;
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error checking authentication attribute for function {FunctionName}",
                context.FunctionDefinition.Name
            );
            return false;
        }
    }

    private RequireTeamRoleAttribute? GetTeamRoleRequirement(FunctionContext context)
    {
        try
        {
            var functionName = context.FunctionDefinition.Name;
            var assembly = Assembly.GetExecutingAssembly();
            var functionTypes = assembly
                .GetTypes()
                .Where(t =>
                    t.GetMethods()
                        .Any(m => m.GetCustomAttribute<FunctionAttribute>()?.Name == functionName)
                );

            foreach (var type in functionTypes)
            {
                var method = type.GetMethods()
                    .FirstOrDefault(m =>
                        m.GetCustomAttribute<FunctionAttribute>()?.Name == functionName
                    );

                if (method != null)
                {
                    return method.GetCustomAttribute<RequireTeamRoleAttribute>();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error checking team role attribute for function {FunctionName}",
                context.FunctionDefinition.Name
            );
            return null;
        }
    }

    private string? ExtractTeamIdFromRoute(HttpRequest request, string routeParam)
    {
        try
        {
            // Try to get from route values
            var routeValues = request.RouteValues;
            if (routeValues != null && routeValues.TryGetValue(routeParam, out var teamIdValue))
            {
                return teamIdValue?.ToString();
            }

            // Fallback: try to parse from path segments
            var pathSegments = request.Path.Value?.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );
            if (pathSegments != null)
            {
                for (int i = 0; i < pathSegments.Length - 1; i++)
                {
                    if (
                        pathSegments[i].Equals("teams", StringComparison.OrdinalIgnoreCase)
                        && i + 1 < pathSegments.Length
                    )
                    {
                        return pathSegments[i + 1];
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting team ID from route");
            return null;
        }
    }

    private static bool IsGlobalAdmin(string? sidespinsRole)
    {
        return string.Equals(sidespinsRole, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAtLeast(string? userRole, string requiredRole)
    {
        var roleHierarchy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["player"] = 1,
            ["captain"] = 2,
            ["manager"] = 2, // captain and manager are equivalent
            ["admin"] = 3,
        };

        return roleHierarchy.GetValueOrDefault(userRole ?? "", 0)
            >= roleHierarchy.GetValueOrDefault(requiredRole, int.MaxValue);
    }

    private async Task WriteUnauthorizedResponse(HttpResponse response)
    {
        response.StatusCode = (int)HttpStatusCode.Unauthorized;
        response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "Unauthorized",
            message = "Valid authentication token required",
        };
        await response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    private async Task WriteForbiddenResponse(
        HttpResponse response,
        AuthorizationErrorResponse? errorResponse = null
    )
    {
        response.StatusCode = (int)HttpStatusCode.Forbidden;
        response.ContentType = "application/json";

        var responseObject =
            errorResponse
            ?? new AuthorizationErrorResponse
            {
                Message = "Insufficient permissions for this operation",
            };

        await response.WriteAsync(JsonSerializer.Serialize(responseObject));
    }
}
