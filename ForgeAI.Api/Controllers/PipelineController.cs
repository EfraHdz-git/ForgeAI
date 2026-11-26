using ForgeAI.Core.DTOs;
using ForgeAI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly IOpportunityRepository _opportunities;
    private readonly IPipelineLogRepository _pipelineLog;
    private readonly IAgentRunRepository _agentRuns;

    public PipelineController(
        IOpportunityRepository opportunities,
        IPipelineLogRepository pipelineLog,
        IAgentRunRepository agentRuns)
    {
        _opportunities = opportunities;
        _pipelineLog = pipelineLog;
        _agentRuns = agentRuns;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var statuses = new[] { "pending_evaluation", "watching", "prototype_queued", "prototyping",
            "deployed", "pending_review", "approved", "rejected" };

        var counts = new PipelineStatusDto();
        foreach (var status in statuses)
        {
            var items = await _opportunities.GetByStatusAsync(status);
            var count = items.Count();
            switch (status)
            {
                case "pending_evaluation": counts.PendingEvaluation = count; break;
                case "watching": counts.Evaluated = count; break;
                case "prototype_queued": counts.PrototypeQueued = count; break;
                case "deployed": counts.Deployed = count; break;
                case "pending_review": counts.PendingReview = count; break;
                case "approved": counts.Approved = count; break;
                case "rejected": counts.Rejected = count; break;
            }
            counts.TotalOpportunities += count;
        }

        return Ok(counts);
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int count = 50)
    {
        var logs = await _pipelineLog.GetRecentAsync(count);
        return Ok(logs);
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetAgentRuns([FromQuery] int count = 50)
    {
        var runs = await _agentRuns.GetRecentAsync(count);
        return Ok(runs);
    }
}
