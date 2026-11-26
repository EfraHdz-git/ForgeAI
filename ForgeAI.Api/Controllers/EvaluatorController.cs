using ForgeAI.Core.Interfaces;
using ForgeAI.Services.Agents;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvaluatorController : ControllerBase
{
    private readonly EvaluatorAgent _evaluator;
    private readonly IEvaluationRepository _evaluations;
    private readonly IOpportunityRepository _opportunities;

    public EvaluatorController(
        EvaluatorAgent evaluator,
        IEvaluationRepository evaluations,
        IOpportunityRepository opportunities)
    {
        _evaluator = evaluator;
        _evaluations = evaluations;
        _opportunities = opportunities;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        var count = await _evaluator.RunAsync();
        return Ok(new { Evaluated = count });
    }

    [HttpGet("{opportunityId}")]
    public async Task<IActionResult> GetEvaluation(int opportunityId)
    {
        var evaluation = await _evaluations.GetByOpportunityIdAsync(opportunityId);
        if (evaluation == null) return NotFound();
        return Ok(evaluation);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _opportunities.GetByStatusAsync("pending_evaluation");
        return Ok(pending);
    }
}
