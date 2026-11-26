using ForgeAI.Core.Interfaces;
using ForgeAI.Services.Agents;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrototypeController : ControllerBase
{
    private readonly PrototypeAgent _prototypeAgent;
    private readonly IPrototypeRepository _prototypes;
    private readonly IOpportunityRepository _opportunities;

    public PrototypeController(
        PrototypeAgent prototypeAgent,
        IPrototypeRepository prototypes,
        IOpportunityRepository opportunities)
    {
        _prototypeAgent = prototypeAgent;
        _prototypes = prototypes;
        _opportunities = opportunities;
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunAll()
    {
        var count = await _prototypeAgent.RunAsync();
        return Ok(new { Generated = count });
    }

    [HttpPost("generate/{opportunityId}")]
    public async Task<IActionResult> GenerateForOpportunity(int opportunityId)
    {
        var opportunity = await _opportunities.GetByIdAsync(opportunityId);
        if (opportunity == null) return NotFound("Opportunity not found");

        // Force status to prototype_queued so the agent picks it up
        await _opportunities.UpdateStatusAsync(opportunityId, "prototype_queued");

        var count = await _prototypeAgent.RunAsync();
        return Ok(new { OpportunityId = opportunityId, Generated = count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPrototype(int id)
    {
        var prototype = await _prototypes.GetByIdAsync(id);
        if (prototype == null) return NotFound();
        return Ok(prototype);
    }

    [HttpGet("opportunity/{opportunityId}")]
    public async Task<IActionResult> GetByOpportunity(int opportunityId)
    {
        var prototype = await _prototypes.GetByOpportunityIdAsync(opportunityId);
        if (prototype == null) return NotFound();
        return Ok(prototype);
    }

    [HttpPost("deploy/{opportunityId}")]
    public async Task<IActionResult> Deploy(int opportunityId)
    {
        var prototype = await _prototypes.GetByOpportunityIdAsync(opportunityId);
        if (prototype == null) return NotFound("No prototype found");
        if (!prototype.BuildReady) return BadRequest("Prototype is not build-ready");

        // Patch index.html to use relative asset paths (Vite builds with absolute / paths)
        PatchIndexHtml(prototype.BuildOutputPath ?? prototype.LocalProjectPath + "\\dist");

        var previewUrl = $"/preview/{opportunityId}/";
        await _prototypes.UpdateDeploymentAsync(prototype.Id, previewUrl, previewUrl);

        // Only change status if not already in review flow
        var opp = await _opportunities.GetByIdAsync(opportunityId);
        if (opp != null && opp.Status != "pending_review")
            await _opportunities.UpdateStatusAsync(opportunityId, "pending_review");

        return Ok(new { OpportunityId = opportunityId, PreviewUrl = previewUrl });
    }

    private static void PatchIndexHtml(string distPath)
    {
        var indexPath = Path.Combine(distPath, "index.html");
        if (!System.IO.File.Exists(indexPath)) return;

        var html = System.IO.File.ReadAllText(indexPath);
        html = html.Replace("href=\"/assets/", "href=\"./assets/");
        html = html.Replace("src=\"/assets/", "src=\"./assets/");
        html = html.Replace("href=\"/vite.svg\"", "href=\"./vite.svg\"");
        System.IO.File.WriteAllText(indexPath, html);
    }

    [HttpPost("deploy-all")]
    public async Task<IActionResult> DeployAll()
    {
        var built = await _opportunities.GetByStatusAsync("prototype_built");
        var deployed = new List<object>();

        foreach (var opp in built)
        {
            var proto = await _prototypes.GetByOpportunityIdAsync(opp.Id);
            if (proto == null || !proto.BuildReady) continue;

            var previewUrl = $"/preview/{opp.Id}/";
            PatchIndexHtml(proto.BuildOutputPath ?? proto.LocalProjectPath + "\\dist");
            await _prototypes.UpdateDeploymentAsync(proto.Id, previewUrl, previewUrl);
            await _opportunities.UpdateStatusAsync(opp.Id, "deployed");
            deployed.Add(new { opp.Id, opp.Title, PreviewUrl = previewUrl });
        }

        return Ok(new { Deployed = deployed.Count, Prototypes = deployed });
    }
}
