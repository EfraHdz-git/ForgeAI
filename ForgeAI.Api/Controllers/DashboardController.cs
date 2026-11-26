using Dapper;
using ForgeAI.Api.Middleware;
using ForgeAI.Core.Interfaces;
using ForgeAI.Data;
using ForgeAI.Services.Agents;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DbConnectionFactory _db;
    private readonly IOpportunityRepository _opportunities;
    private readonly IPipelineLogRepository _pipelineLog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public DashboardController(
        DbConnectionFactory db,
        IOpportunityRepository opportunities,
        IPipelineLogRepository pipelineLog,
        IServiceScopeFactory scopeFactory,
        IConfiguration config)
    {
        _db = db;
        _opportunities = opportunities;
        _pipelineLog = pipelineLog;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        using var conn = _db.CreateConnection();
        var counts = await conn.QueryAsync<StatusCount>(
            "SELECT status, COUNT(*) as count FROM opportunities GROUP BY status");

        var signalsToday = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM raw_signals WHERE discovered_at >= CAST(GETUTCDATE() AS DATE)");

        var prototypesBuilt = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM prototypes WHERE build_ready = 1");

        var dict = counts.ToDictionary(c => c.Status, c => c.Count);

        return Ok(new
        {
            SignalsToday = signalsToday,
            OpportunitiesTotal = dict.Values.Sum(),
            PendingEvaluation = dict.GetValueOrDefault("pending_evaluation", 0),
            Watching = dict.GetValueOrDefault("watching", 0),
            Rejected = dict.GetValueOrDefault("rejected", 0),
            PrototypeQueued = dict.GetValueOrDefault("prototype_queued", 0),
            Prototyping = dict.GetValueOrDefault("prototyping", 0),
            PrototypeBuilt = dict.GetValueOrDefault("prototype_built", 0),
            Deployed = dict.GetValueOrDefault("deployed", 0),
            PendingReview = dict.GetValueOrDefault("pending_review", 0),
            RevisionRequested = dict.GetValueOrDefault("revision_requested", 0),
            Approved = dict.GetValueOrDefault("approved", 0) + dict.GetValueOrDefault("approved_for_mvp", 0),
            Dismissed = dict.GetValueOrDefault("dismissed", 0),
            PrototypesBuilt = prototypesBuilt
        });
    }

    [HttpGet("opportunities")]
    public async Task<IActionResult> GetAllOpportunities(
        [FromQuery] string? status = null,
        [FromQuery] int? minScore = null,
        [FromQuery] int? maxScore = null)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            SELECT o.*, e.overall_score, e.recommendation, e.summary as evaluation_summary,
                   p.status as prototype_status, p.deploy_url, p.build_ready,
                   p.build_started_at, p.build_finished_at
            FROM opportunities o
            LEFT JOIN evaluations e ON e.opportunity_id = o.id
                AND e.id = (SELECT MAX(id) FROM evaluations WHERE opportunity_id = o.id)
            LEFT JOIN prototypes p ON p.opportunity_id = o.id
                AND p.id = (SELECT MAX(id) FROM prototypes WHERE opportunity_id = o.id)
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(status))
        {
            sql += " AND o.status = @Status";
            parameters.Add("Status", status);
        }
        if (minScore.HasValue)
        {
            sql += " AND e.overall_score >= @MinScore";
            parameters.Add("MinScore", minScore.Value);
        }
        if (maxScore.HasValue)
        {
            sql += " AND e.overall_score <= @MaxScore";
            parameters.Add("MaxScore", maxScore.Value);
        }

        sql += " ORDER BY o.created_at DESC";

        var results = await conn.QueryAsync(sql, parameters);
        return Ok(results);
    }

    [HttpGet("opportunities/{id}")]
    public async Task<IActionResult> GetOpportunityDetail(int id)
    {
        using var conn = _db.CreateConnection();

        var opportunity = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM opportunities WHERE id = @Id", new { Id = id });
        if (opportunity == null) return NotFound();

        var evaluation = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM evaluations WHERE opportunity_id = @Id ORDER BY evaluated_at DESC",
            new { Id = id });

        var prototype = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM prototypes WHERE opportunity_id = @Id ORDER BY created_at DESC",
            new { Id = id });

        var cluster = opportunity.cluster_id != null
            ? await conn.QuerySingleOrDefaultAsync(
                "SELECT * FROM opportunity_clusters WHERE id = @Id",
                new { Id = (int)opportunity.cluster_id })
            : null;

        var signals = opportunity.cluster_id != null
            ? await conn.QueryAsync(
                "SELECT * FROM raw_signals WHERE cluster_id = @ClusterId",
                new { ClusterId = (int)opportunity.cluster_id })
            : Enumerable.Empty<dynamic>();

        var timeline = await conn.QueryAsync(
            "SELECT * FROM pipeline_logs WHERE opportunity_id = @Id ORDER BY timestamp ASC",
            new { Id = id });

        var feedback = await conn.QueryAsync(
            "SELECT * FROM decision_feedback WHERE opportunity_id = @Id ORDER BY created_at DESC",
            new { Id = id });

        var review = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM reviews WHERE opportunity_id = @Id ORDER BY created_at DESC",
            new { Id = id });

        return Ok(new
        {
            Opportunity = opportunity,
            Evaluation = evaluation,
            Prototype = prototype,
            Cluster = cluster,
            Signals = signals,
            Timeline = timeline,
            Feedback = feedback,
            Review = review
        });
    }

    [HttpGet("opportunities/{id}/report")]
    public async Task<IActionResult> GetReport(int id)
    {
        using var conn = _db.CreateConnection();
        var opportunity = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM opportunities WHERE id = @Id", new { Id = id });
        if (opportunity == null) return NotFound();

        var evaluation = await conn.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 * FROM evaluations WHERE opportunity_id = @Id ORDER BY evaluated_at DESC",
            new { Id = id });

        var prototype = await conn.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 * FROM prototypes WHERE opportunity_id = @Id ORDER BY created_at DESC",
            new { Id = id });

        // Read market report JSON from disk
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

    [HttpPost("opportunities/submit")]
    public async Task<IActionResult> SubmitIdea([FromBody] ManualIdeaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Problem))
            return BadRequest(new { Error = "Problem description is required." });

        // Auto-generate title if not provided (public form sends problem/who/when, no title)
        var title = request.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            try
            {
                using var titleScope = _scopeFactory.CreateScope();
                var bedrock = titleScope.ServiceProvider.GetRequiredService<ForgeAI.Core.Interfaces.IBedrockService>();
                var solutionCtx = !string.IsNullOrWhiteSpace(request.UseCase) ? $"\nImagined solution: {request.UseCase}" : "";
                title = (await bedrock.InvokeAsync(
                    $"Generate a short product name (2-4 words, no quotes, no punctuation) for this idea:\nProblem: {request.Problem}\nUser: {request.Who}{solutionCtx}\nReturn ONLY the product name, nothing else."
                )).Trim().Trim('"').Trim('\'');
            }
            catch
            {
                title = request.Problem.Length > 50
                    ? request.Problem[..50].Trim() + "..."
                    : request.Problem;
            }
        }

        var opportunity = new ForgeAI.Core.Models.Opportunity
        {
            Title = title,
            PlainTitle = title,
            ProblemStatement = request.Problem,
            TargetAudience = request.TargetAudience ?? request.Who,
            WhyNow = request.WhyNow,
            WhyMobileFirst = request.Platform,
            UserType = request.UserType ?? request.Who,
            SimpleUseCase = request.UseCase,
            MomentOfPain = request.When,
            UserId = HttpContext.GetUserId(),
            Status = "pending_evaluation"
        };

        var id = await _opportunities.CreateAsync(opportunity);
        await _pipelineLog.LogAsync("manual_public", id, "idea_submitted",
            $"Public submission: {title}");

        await SseController.BroadcastAsync("opportunity.created", new
        {
            OpportunityId = id,
            Title = title,
            Source = "manual_public"
        });

        // Kick off evaluation pipeline in background (don't wait for weekly cycle)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var evaluator = scope.ServiceProvider.GetRequiredService<EvaluatorAgent>();
                await evaluator.EvaluateSingleAsync(id, alwaysRunMarketResearch: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Background submission pipeline failed for {id}: {ex.Message}");
                await SseController.BroadcastAsync("opportunity.stage", new
                {
                    OpportunityId = id,
                    Stage = "failed",
                    Error = "Analysis failed. Please try again."
                });
            }
        });

        return Ok(new { Id = id, Status = "processing" });
    }

    [HttpPost("opportunities/{id}/promote")]
    public async Task<IActionResult> PromoteToPrototype(int id, [FromBody] PromoteRequest request)
    {
        var opportunity = await _opportunities.GetByIdAsync(id);
        if (opportunity == null) return NotFound();

        var previousStatus = opportunity.Status;
        await _opportunities.UpdateStatusAsync(id, "prototype_queued");

        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE opportunities SET was_manual_override = 1, override_reason = @Reason, override_at = GETUTCDATE()
            WHERE id = @Id",
            new { Id = id, Reason = request.Reason ?? "Manual override" });

        await _pipelineLog.LogAsync("manual_override", id, "promoted_to_prototype",
            $"Previous status: {previousStatus}. Reason: {request.Reason ?? "Manual override"}");

        await SseController.BroadcastAsync("opportunity.promoted", new
        {
            OpportunityId = id,
            Title = opportunity.Title,
            PreviousStatus = previousStatus,
            Reason = request.Reason
        });

        // Fire prototype build in background — don't wait for weekly cycle
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var protoAgent = scope.ServiceProvider.GetRequiredService<PrototypeAgent>();
                await protoAgent.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Background prototype build failed: {ex.Message}");
            }
        });

        return Ok(new { Id = id, PreviousStatus = previousStatus, NewStatus = "prototype_queued" });
    }

    [HttpPost("opportunities/{id}/dismiss")]
    public async Task<IActionResult> Dismiss(int id, [FromBody] DismissRequest request)
    {
        var opportunity = await _opportunities.GetByIdAsync(id);
        if (opportunity == null) return NotFound();

        await _opportunities.UpdateStatusAsync(id, "dismissed");
        await _pipelineLog.LogAsync("review", id, "dismissed", request.Reason ?? "Dismissed by operator");

        return Ok(new { Id = id, Status = "dismissed" });
    }

    [HttpPost("opportunities/{id}/reopen")]
    public async Task<IActionResult> Reopen(int id)
    {
        var opportunity = await _opportunities.GetByIdAsync(id);
        if (opportunity == null) return NotFound();

        await _opportunities.UpdateStatusAsync(id, "pending_evaluation");
        await _pipelineLog.LogAsync("manual_override", id, "reopened",
            $"Reopened from status: {opportunity.Status}");

        return Ok(new { Id = id, Status = "pending_evaluation" });
    }

    [HttpGet("pipeline/status")]
    public async Task<IActionResult> GetPipelineStatus()
    {
        using var conn = _db.CreateConnection();
        var running = await conn.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM agent_runs WHERE status = 'running' AND job_type IN ('scout', 'evaluator') AND started_at > DATEADD(HOUR, -1, GETUTCDATE())");
        return Ok(new { Running = running > 0 });
    }

    [HttpGet("focus-profiles")]
    public async Task<IActionResult> GetFocusProfiles()
    {
        using var conn = _db.CreateConnection();
        var profiles = await conn.QueryAsync(
            "SELECT id, name, display_name, is_active, platform_bias FROM focus_profiles ORDER BY id");
        return Ok(profiles);
    }

    [HttpPost("pipeline/run")]
    public async Task<IActionResult> RunPipeline([FromBody] PipelineRunRequest request)
    {
        if (request.ProfileIds == null || request.ProfileIds.Length == 0)
            return BadRequest(new { Error = "Select at least one niche." });

        await _pipelineLog.LogAsync("manual_pipeline", null, "pipeline_triggered",
            $"Manual run for profiles: [{string.Join(", ", request.ProfileIds)}]");

        await SseController.BroadcastAsync("pipeline.started", new
        {
            ProfileIds = request.ProfileIds,
            Source = "manual"
        });

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                // Step 1: Scout with selected profiles
                var scout = scope.ServiceProvider.GetRequiredService<ScoutAgent>();
                var scoutResult = await scout.RunAsync(request.ProfileIds);

                await SseController.BroadcastAsync("pipeline.scout_done", new
                {
                    Signals = scoutResult.SignalsDiscovered,
                    Clusters = scoutResult.ClustersCreated,
                    Opportunities = scoutResult.OpportunitiesCreated
                });

                // Step 2: Evaluate pending opportunities
                var evaluator = scope.ServiceProvider.GetRequiredService<EvaluatorAgent>();
                var evaluated = await evaluator.RunAsync();

                await SseController.BroadcastAsync("pipeline.completed", new
                {
                    Evaluated = evaluated,
                    Signals = scoutResult.SignalsDiscovered,
                    Opportunities = scoutResult.OpportunitiesCreated
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Manual pipeline run failed: {ex.Message}");
                await SseController.BroadcastAsync("pipeline.failed", new { Error = ex.Message });
            }
        });

        return Ok(new { Status = "running", ProfileIds = request.ProfileIds });
    }

    private class StatusCount
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
    }
}

public class ManualIdeaRequest
{
    // V2 public form: 3 fields
    public string Problem { get; set; } = "";
    public string Who { get; set; } = "";
    public string? When { get; set; }

    // V1 legacy fields (still accepted for internal use)
    public string? Title { get; set; }
    public string? TargetAudience { get; set; }
    public string? WhyNow { get; set; }
    public string? Platform { get; set; }
    public string? UserType { get; set; }
    public string? UseCase { get; set; }
}

public class PromoteRequest
{
    public string? Reason { get; set; }
}

public class DismissRequest
{
    public string? Reason { get; set; }
}

public class PipelineRunRequest
{
    public int[] ProfileIds { get; set; } = [];
}
