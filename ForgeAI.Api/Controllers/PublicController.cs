using Dapper;
using ForgeAI.Data;
using ForgeAI.Services.Sharing;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicController : ControllerBase
{
    private readonly DbConnectionFactory _db;
    private readonly IConfiguration _config;
    private readonly ShareService _share;

    public PublicController(DbConnectionFactory db, IConfiguration config, ShareService share)
    {
        _db = db;
        _config = config;
        _share = share;
    }

    [HttpGet("report/{token}")]
    public async Task<IActionResult> GetSharedReport(string token)
    {
        var shareToken = await _share.ValidateAsync(token);
        if (shareToken == null || shareToken.Type != "report")
            return NotFound(new { Error = "This link is invalid or expired." });

        var id = shareToken.OpportunityId;
        using var conn = _db.CreateConnection();

        var opportunity = await conn.QuerySingleOrDefaultAsync(
            "SELECT id, title, plain_title, problem_statement, target_audience, why_now, why_mobile_first, user_type, moment_of_pain, simple_use_case, created_at FROM opportunities WHERE id = @Id",
            new { Id = id });
        if (opportunity == null) return NotFound();

        var evaluation = await conn.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 * FROM evaluations WHERE opportunity_id = @Id ORDER BY evaluated_at DESC",
            new { Id = id });

        var prototype = await conn.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 id, status FROM prototypes WHERE opportunity_id = @Id ORDER BY created_at DESC",
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
            MarketReport = marketReport,
            Shared = true
        });
    }
}
