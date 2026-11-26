using Dapper;
using ForgeAI.Api.Middleware;
using ForgeAI.Data;
using ForgeAI.Services.Sharing;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly DbConnectionFactory _db;
    private readonly IConfiguration _config;
    private readonly ShareService _share;

    public UserController(DbConnectionFactory db, IConfiguration config, ShareService share)
    {
        _db = db;
        _config = config;
        _share = share;
    }

    [HttpGet("ideas")]
    public async Task<IActionResult> GetMyIdeas()
    {
        var userId = HttpContext.GetUserId();
        if (userId == null) return Unauthorized(new { Error = "Not authenticated." });

        using var conn = _db.CreateConnection();
        var ideas = await conn.QueryAsync(@"
            SELECT o.id, o.title, o.plain_title, o.problem_statement, o.status, o.created_at,
                   e.overall_score, e.summary as evaluation_summary, e.recommendation,
                   e.market_fit_score, e.competition_score, e.monetization_score,
                   e.evaluated_at,
                   p.deploy_url, p.status as prototype_status, p.build_finished_at
            FROM opportunities o
            LEFT JOIN evaluations e ON e.opportunity_id = o.id
                AND e.id = (SELECT MAX(id) FROM evaluations WHERE opportunity_id = o.id)
            LEFT JOIN prototypes p ON p.opportunity_id = o.id
                AND p.id = (SELECT MAX(id) FROM prototypes WHERE opportunity_id = o.id)
            WHERE o.user_id = @UserId
            ORDER BY o.created_at DESC", new { UserId = userId });

        // Attach market research summary per idea
        var result = new List<object>();
        foreach (var idea in ideas)
        {
            object? marketMeta = null;
            var researchPath = System.IO.Path.Combine(
                _config["Pipeline:ResearchBasePath"] ?? "/var/www/api/forgeai/research",
                $"research-{idea.id}", "market-report.json");
            if (System.IO.File.Exists(researchPath))
            {
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(researchPath);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    marketMeta = new
                    {
                        competitor_count = root.TryGetProperty("competitor_count_found", out var cc) ? cc.GetInt32() : 0,
                        competition_level = root.TryGetProperty("competition_level", out var cl) ? cl.GetString() : null,
                        market_maturity = root.TryGetProperty("market_maturity", out var mm) ? mm.GetString() : null
                    };
                }
                catch { }
            }

            result.Add(new { idea, market = marketMeta });
        }

        return Ok(result);
    }

    [HttpGet("ideas/{id}/report")]
    public async Task<IActionResult> GetMyReport(int id)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null) return Unauthorized(new { Error = "Not authenticated." });

        using var conn = _db.CreateConnection();

        var opportunity = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM opportunities WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
        if (opportunity == null) return NotFound(new { Error = "Idea not found." });

        var evaluation = await conn.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 * FROM evaluations WHERE opportunity_id = @Id ORDER BY evaluated_at DESC",
            new { Id = id });

        var prototype = await conn.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 * FROM prototypes WHERE opportunity_id = @Id ORDER BY created_at DESC",
            new { Id = id });

        object? marketReport = null;
        var researchPath = System.IO.Path.Combine(
            _config["Pipeline:ResearchBasePath"] ?? "/var/www/api/forgeai/research",
            $"research-{id}", "market-report.json");
        if (System.IO.File.Exists(researchPath))
        {
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(researchPath);
                marketReport = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
            }
            catch { }
        }

        return Ok(new
        {
            Opportunity = opportunity,
            Evaluation = evaluation,
            Prototype = prototype,
            MarketReport = marketReport
        });
    }

    [HttpPost("ideas/{id}/share")]
    public async Task<IActionResult> ShareReport(int id, [FromBody] ShareRequest? request)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null) return Unauthorized(new { Error = "Not authenticated." });

        var type = request?.Type == "prototype" ? "prototype" : "report";
        var result = await _share.CreateShareAsync(id, userId.Value, type);
        if (result == null) return NotFound(new { Error = "Idea not found." });

        return Ok(result);
    }

    [HttpGet("ideas/{id}/shares")]
    public async Task<IActionResult> GetShares(int id)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null) return Unauthorized(new { Error = "Not authenticated." });

        // Ownership check
        using var conn = _db.CreateConnection();
        var owns = await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM opportunities WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
        if (owns == null) return NotFound();

        var shares = await _share.GetSharesForOpportunityAsync(id);
        return Ok(shares);
    }

    [HttpDelete("shares/{shareId}")]
    public async Task<IActionResult> RevokeShare(int shareId)
    {
        var userId = HttpContext.GetUserId();
        if (userId == null) return Unauthorized(new { Error = "Not authenticated." });

        // Verify ownership via the share's user_id
        using var conn = _db.CreateConnection();
        var ownerId = await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT user_id FROM share_tokens WHERE id = @Id", new { Id = shareId });
        if (ownerId == null || ownerId != userId) return NotFound();

        await _share.RevokeAsync(shareId);
        return Ok(new { Revoked = true });
    }
}

public class ShareRequest
{
    public string? Type { get; set; }
}
