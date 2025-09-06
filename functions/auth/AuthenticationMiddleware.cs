using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace SidesSpins.Functions;

public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly AuthService _authService;

    public AuthenticationMiddleware(ILogger<AuthenticationMiddleware> logger, AuthService authService)
    {
        _logger = logger;
        _authService = authService;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            await next(context);
            return;
        }

        // Check if function requires authentication
        var requiresAuth = HasAuthenticationAttribute(context);

        if (!requiresAuth)
        {
            await next(context);
            return;
        }

        try
        {
            // Extract JWT from request
            var jwt = ExtractJwtFromRequest(httpContext.Request);
            if (string.IsNullOrEmpty(jwt))
            {
                await WriteUnauthorizedResponse(httpContext.Response);
                return;
            }

            // Validate App JWT
            var (isValid, claims) = _authService.ValidateAppJwt(jwt);
            if (!isValid || claims == null)
            {
                await WriteUnauthorizedResponse(httpContext.Response);
                return;
            }

            // Check role-based access
            if (!ValidateRoleAccess(claims, context))
            {
                await WriteForbiddenResponse(httpContext.Response);
                return;
            }

            // Add claims to context for use in function
            context.Items["UserClaims"] = claims;
            context.Items["UserId"] = claims.Sub;
            context.Items["TeamId"] = claims.TeamId;
            context.Items["TeamRole"] = claims.TeamRole;

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

        // Check cookies
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
            var functionTypes = assembly.GetTypes()
                .Where(t => t.GetMethods().Any(m => m.GetCustomAttribute<FunctionAttribute>()?.Name == functionName));

            foreach (var type in functionTypes)
            {
                var method = type.GetMethods()
                    .FirstOrDefault(m => m.GetCustomAttribute<FunctionAttribute>()?.Name == functionName);

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
            _logger.LogWarning(ex, "Error checking authentication attribute for function {FunctionName}", context.FunctionDefinition.Name);
            return false;
        }
    }

    private bool ValidateRoleAccess(AppClaims claims, FunctionContext context)
    {
        if (!context.Items.TryGetValue("RequiredRole", out var requiredRoleObj))
            return true; // No specific role required

        var requiredRole = requiredRoleObj?.ToString();
        if (string.IsNullOrEmpty(requiredRole))
            return true;

        return HasRequiredRole(claims.TeamRole, requiredRole);
    }

    private bool HasRequiredRole(string userRole, string requiredRole)
    {
        // Define role hierarchy
        var roleHierarchy = new Dictionary<string, int>
        {
            { "player", 1 },
            { "manager", 2 },
            { "admin", 3 }
        };

        return roleHierarchy.GetValueOrDefault(userRole, 0) >= 
               roleHierarchy.GetValueOrDefault(requiredRole, 0);
    }

    private async Task WriteUnauthorizedResponse(HttpResponse response)
    {
        response.StatusCode = (int)HttpStatusCode.Unauthorized;
        response.ContentType = "application/json";
        
        var errorResponse = new { error = "Unauthorized", message = "Valid authentication token required" };
        await response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    private async Task WriteForbiddenResponse(HttpResponse response)
    {
        response.StatusCode = (int)HttpStatusCode.Forbidden;
        response.ContentType = "application/json";
        
        var errorResponse = new { error = "Forbidden", message = "Insufficient permissions for this operation" };
        await response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }
}
