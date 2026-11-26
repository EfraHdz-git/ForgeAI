using Dapper;
using ForgeAI.Data;
using ForgeAI.Services.Sharing;

namespace ForgeAI.Api.Middleware;

public class PreviewMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _prototypesBasePath;
    private readonly ILogger<PreviewMiddleware> _logger;

    public PreviewMiddleware(RequestDelegate next, IConfiguration config, ILogger<PreviewMiddleware> logger)
    {
        _next = next;
        _prototypesBasePath = config["Pipeline:PrototypesBasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prototypes");
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ShareService share, DbConnectionFactory db)
    {
        var path = context.Request.Path.Value;

        if (path == null || !path.StartsWith("/preview/"))
        {
            await _next(context);
            return;
        }

        // Parse /preview/{id}/{rest}
        var segments = path.TrimStart('/').Split('/', 3);
        if (segments.Length < 2 || !int.TryParse(segments[1], out var opportunityId))
        {
            await _next(context);
            return;
        }

        // Check access — JWT owner, share token, or existing session cookie
        var accessResult = await CheckAccessAsync(context, share, db, opportunityId);
        if (!accessResult.Allowed)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("This prototype requires a valid access link.");
            return;
        }

        // If this was the first grant (via token or JWT), set a cookie for asset requests
        if (accessResult.SetCookie)
        {
            context.Response.Cookies.Append($"prev_{opportunityId}", "ok", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = TimeSpan.FromHours(1)
            });
        }

        await ServePrototypeFileAsync(context, opportunityId, segments);
    }

    private record AccessResult(bool Allowed, bool SetCookie);

    private async Task<AccessResult> CheckAccessAsync(HttpContext context, ShareService share, DbConnectionFactory db, int opportunityId)
    {
        // 1. Existing preview cookie (asset requests after initial grant)
        if (context.Request.Cookies.ContainsKey($"prev_{opportunityId}"))
            return new AccessResult(true, false);

        // 2. Share token in query string
        var token = context.Request.Query["share"].ToString();
        if (!string.IsNullOrWhiteSpace(token))
        {
            var record = await share.ValidateAsync(token);
            if (record != null && record.OpportunityId == opportunityId && record.Type == "prototype")
                return new AccessResult(true, true);
        }

        // 3. JWT owner
        var userId = context.Items["UserId"] as int?;
        if (userId != null)
        {
            using var conn = db.CreateConnection();
            var ownerId = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT user_id FROM opportunities WHERE id = @Id", new { Id = opportunityId });
            if (ownerId == userId) return new AccessResult(true, true);
        }

        // 4. Legacy internal opps (user_id = null) are still open (admin view)
        using (var conn = db.CreateConnection())
        {
            var hasOwner = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT user_id FROM opportunities WHERE id = @Id", new { Id = opportunityId });
            if (hasOwner == null) return new AccessResult(true, false);
        }

        return new AccessResult(false, false);
    }

    private async Task ServePrototypeFileAsync(HttpContext context, int opportunityId, string[] segments)
    {
        var basePath = Path.GetFullPath(_prototypesBasePath);
        var candidates = Directory.Exists(basePath)
            ? Directory.GetDirectories(basePath, $"prototype-{opportunityId}*")
                .OrderByDescending(d => d.Length)
                .ThenByDescending(d => d)
                .ToList()
            : new List<string>();

        var protoDir = candidates.FirstOrDefault(d => Directory.Exists(Path.Combine(d, "dist")))
            ?? Path.Combine(basePath, $"prototype-{opportunityId}");
        var distPath = Path.Combine(protoDir, "dist");

        if (!Directory.Exists(distPath))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"No preview found for opportunity {opportunityId}");
            return;
        }

        var relativePath = segments.Length > 2 ? segments[2] : "index.html";
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/") relativePath = "index.html";

        var filePath = Path.GetFullPath(Path.Combine(distPath, relativePath));

        if (!filePath.StartsWith(distPath))
        {
            context.Response.StatusCode = 403;
            return;
        }

        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(distPath, "index.html");
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                return;
            }
        }

        context.Response.ContentType = GetContentType(filePath);
        await context.Response.SendFileAsync(filePath);
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            _ => "application/octet-stream"
        };
    }
}
