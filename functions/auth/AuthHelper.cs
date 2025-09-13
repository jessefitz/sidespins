using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SideSpins.Api.Helpers;

public static class AuthHelper
{
    public static IActionResult? ValidateApiSecret(HttpRequest req)
    {
        var secret = req.Headers["x-api-secret"].FirstOrDefault();
        var expected = Environment.GetEnvironmentVariable("API_SHARED_SECRET");

        if (string.IsNullOrEmpty(secret) || secret != expected)
        {
            return new UnauthorizedResult();
        }

        return null;
    }
}
