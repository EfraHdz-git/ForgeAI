using ForgeAI.Services.Auth;

namespace ForgeAI.Api.Middleware;

public class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;

    public JwtAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, JwtService jwt)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];
            var principal = jwt.ValidateToken(token);

            if (principal != null)
            {
                var userId = jwt.GetUserIdFromPrincipal(principal);
                var email = principal.FindFirst("email")?.Value;
                var tier = principal.FindFirst("tier")?.Value;

                var admin = principal.FindFirst("admin")?.Value;

                context.Items["UserId"] = userId;
                context.Items["UserEmail"] = email;
                context.Items["UserTier"] = tier;
                context.Items["IsAdmin"] = admin == "true";
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension to require auth on specific endpoints.
/// Use as an attribute or inline check.
/// </summary>
public static class AuthExtensions
{
    public static int? GetUserId(this HttpContext context)
        => context.Items["UserId"] as int?;

    public static string? GetUserEmail(this HttpContext context)
        => context.Items["UserEmail"] as string;

    public static string? GetUserTier(this HttpContext context)
        => context.Items["UserTier"] as string;

    public static bool IsAuthenticated(this HttpContext context)
        => context.Items["UserId"] != null;

    public static bool IsAdmin(this HttpContext context)
        => context.Items["IsAdmin"] is true;
}
