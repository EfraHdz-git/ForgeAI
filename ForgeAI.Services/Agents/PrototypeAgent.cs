using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using ForgeAI.Services.Prototype;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Agents;

public class PrototypeAgent
{
    private readonly IOpportunityRepository _opportunities;
    private readonly IEvaluationRepository _evaluations;
    private readonly IPrototypeRepository _prototypes;
    private readonly IAgentRunRepository _agentRuns;
    private readonly IPipelineLogRepository _pipelineLog;
    private readonly IDecisionFeedbackRepository _feedback;
    private readonly ISseBroadcaster _sse;
    private readonly PrdGenerator _prdGenerator;
    private readonly ClaudeCodeRunner _claudeCode;
    private readonly ILogger<PrototypeAgent> _logger;

    public PrototypeAgent(
        IOpportunityRepository opportunities,
        IEvaluationRepository evaluations,
        IPrototypeRepository prototypes,
        IAgentRunRepository agentRuns,
        IPipelineLogRepository pipelineLog,
        IDecisionFeedbackRepository feedback,
        ISseBroadcaster sse,
        PrdGenerator prdGenerator,
        ClaudeCodeRunner claudeCode,
        ILogger<PrototypeAgent> logger)
    {
        _opportunities = opportunities;
        _evaluations = evaluations;
        _prototypes = prototypes;
        _agentRuns = agentRuns;
        _pipelineLog = pipelineLog;
        _feedback = feedback;
        _sse = sse;
        _prdGenerator = prdGenerator;
        _claudeCode = claudeCode;
        _logger = logger;
    }

    public async Task<int> RunAsync()
    {
        var agentRun = new AgentRun
        {
            JobType = "prototype_generator",
            Status = "running",
            StartedAt = DateTime.UtcNow
        };
        var runId = await _agentRuns.CreateAsync(agentRun);

        try
        {
            // Handle new prototypes (Gate 1 approved)
            var queued = await _opportunities.GetByStatusAsync("prototype_queued");
            // Handle V2 requests (Gate 2 revision)
            var revisions = await _opportunities.GetByStatusAsync("revision_requested");

            int generated = 0;

            foreach (var opportunity in queued)
            {
                try
                {
                    await GeneratePrototypeAsync(opportunity, version: 1);
                    generated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Prototype generation failed for opportunity {Id}: {Title}",
                        opportunity.Id, opportunity.Title);
                    await _pipelineLog.LogAsync("prototype", opportunity.Id, "generation_failed", ex.Message);
                }
            }

            foreach (var opportunity in revisions)
            {
                try
                {
                    await GenerateV2Async(opportunity);
                    generated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "V2 generation failed for opportunity {Id}: {Title}",
                        opportunity.Id, opportunity.Title);
                    await _pipelineLog.LogAsync("prototype", opportunity.Id, "v2_generation_failed", ex.Message);
                }
            }

            await _agentRuns.UpdateStatusAsync(runId, "completed");
            _logger.LogInformation("Prototype run {RunId} completed. Generated: {Count}", runId, generated);
            return generated;
        }
        catch (Exception ex)
        {
            await _agentRuns.UpdateStatusAsync(runId, "failed", ex.Message);
            throw;
        }
    }

    private async Task GeneratePrototypeAsync(Opportunity opportunity, int version)
    {
        _logger.LogInformation("Starting prototype v{Version} for '{Title}' (ID: {Id})",
            version, opportunity.Title, opportunity.Id);

        var evaluation = await _evaluations.GetByOpportunityIdAsync(opportunity.Id);

        // Step 1: Generate PRD via Bedrock
        await _pipelineLog.LogAsync("prototype", opportunity.Id, "prd_generating",
            $"Generating PRD v{version} for: {opportunity.Title}");
        await _opportunities.UpdateStatusAsync(opportunity.Id, "prototyping");

        var prd = await _prdGenerator.GenerateAsync(opportunity, evaluation);

        // Step 2: Create prototype record
        var prototype = new Core.Models.Prototype
        {
            OpportunityId = opportunity.Id,
            EvaluationId = evaluation?.Id,
            PrdContent = prd,
            VersionNumber = version,
            Status = "generating",
            BuildStartedAt = DateTime.UtcNow
        };
        var protoId = await _prototypes.CreateAsync(prototype);

        await _pipelineLog.LogAsync("prototype", opportunity.Id, "claude_code_starting",
            $"Spawning Claude Code for prototype {protoId} (v{version})");

        // Step 3: Run Claude Code (versioned directory)
        var result = await _claudeCode.RunAsync(opportunity.Id, prd, version: version);

        // Step 4: Update prototype with execution metadata
        await UpdatePrototypeMetadataAsync(protoId, result);

        if (result.Success && result.BuildReady)
        {
            // Step 5: Cleanup node_modules to save disk
            CleanupNodeModules(result.WorkingDirectory);

            // Step 6: Auto-deploy preview
            var previewUrl = $"/preview/{opportunity.Id}/";
            var distPath = Path.Combine(result.WorkingDirectory, "dist");
            var indexPath = Path.Combine(distPath, "index.html");
            if (File.Exists(indexPath))
            {
                var html = await File.ReadAllTextAsync(indexPath);
                html = html.Replace("href=\"/assets/", "href=\"./assets/");
                html = html.Replace("src=\"/assets/", "src=\"./assets/");
                html = html.Replace("href=\"/vite.svg\"", "href=\"./vite.svg\"");
                html = html.Replace("href=\"/favicon.svg\"", "href=\"./favicon.svg\"");
                await File.WriteAllTextAsync(indexPath, html);
            }
            await _prototypes.UpdateDeploymentAsync(protoId, previewUrl, previewUrl);
            await _opportunities.UpdateStatusAsync(opportunity.Id, "pending_review");
            var duration = result.FinishedAt - result.StartedAt;
            var durationStr = duration?.TotalMinutes >= 1
                ? $"{(int)duration.Value.TotalMinutes}m {duration.Value.Seconds}s"
                : $"{duration?.TotalSeconds:F0}s";
            await _pipelineLog.LogAsync("prototype", opportunity.Id, "prototype_built",
                $"v{version} built successfully. Files: {result.GeneratedFiles?.Count ?? 0}, Duration: {durationStr}");

            _logger.LogInformation(
                "Prototype {ProtoId} v{Version} built for '{Title}': {FileCount} files in {Duration}s",
                protoId, version, opportunity.Title, result.GeneratedFiles?.Count ?? 0,
                (result.FinishedAt - result.StartedAt)?.TotalSeconds);

            await _sse.BroadcastAsync("prototype.completed", new
            {
                OpportunityId = opportunity.Id,
                Title = opportunity.Title,
                PreviewUrl = previewUrl,
                Version = version
            });
        }
        else
        {
            var reason = result.TimedOut ? "timed_out" : "build_failed";
            await _prototypes.UpdateStatusAsync(protoId, "failed",
                result.TimedOut ? "Timed out" : $"Exit code: {result.ExitCode}");
            await _opportunities.UpdateStatusAsync(opportunity.Id, "prototype_failed");
            await _pipelineLog.LogAsync("prototype", opportunity.Id, reason,
                $"v{version} failed. Exit: {result.ExitCode}, TimedOut: {result.TimedOut}");

            _logger.LogWarning(
                "Prototype {ProtoId} v{Version} failed for '{Title}': exit={Exit}, timedOut={TimedOut}",
                protoId, version, opportunity.Title, result.ExitCode, result.TimedOut);
        }
    }

    private async Task GenerateV2Async(Opportunity opportunity)
    {
        // Get previous prototype and feedback
        var previousProto = await _prototypes.GetByOpportunityIdAsync(opportunity.Id);
        var feedbackItems = await _feedback.GetByOpportunityIdAsync(opportunity.Id);

        var previousVersion = previousProto?.VersionNumber ?? 1;
        var newVersion = previousVersion + 1;

        // Mark previous prototype as superseded
        if (previousProto != null)
            await _prototypes.UpdateStatusAsync(previousProto.Id, "superseded");

        _logger.LogInformation("Starting V{Version} for '{Title}' based on feedback",
            newVersion, opportunity.Title);

        // Build feedback context for the PRD generator
        var feedbackText = string.Join("\n", feedbackItems.Select(f =>
            $"- [{f.Decision}] {f.ReasonCategory}: {f.Notes}"));

        if (!string.IsNullOrEmpty(feedbackText))
        {
            // Temporarily append feedback to the opportunity's problem statement
            // so the PRD generator can see it
            var originalProblem = opportunity.ProblemStatement;
            opportunity.ProblemStatement = $"{originalProblem}\n\nFEEDBACK FROM V{previousVersion} REVIEW:\n{feedbackText}\n\nPREVIOUS PRD:\n{previousProto?.PrdContent?[..Math.Min(previousProto.PrdContent?.Length ?? 0, 500)] ?? "N/A"}";

            await GeneratePrototypeAsync(opportunity, newVersion);

            // Restore original
            opportunity.ProblemStatement = originalProblem;
        }
        else
        {
            await GeneratePrototypeAsync(opportunity, newVersion);
        }

        await _pipelineLog.LogAsync("prototype", opportunity.Id, "v2_requested",
            $"V{newVersion} generated from revision feedback: {feedbackText?[..Math.Min(feedbackText?.Length ?? 0, 200)]}");
    }

    private void CleanupNodeModules(string projectDir)
    {
        var nodeModulesPath = Path.Combine(projectDir, "node_modules");
        if (Directory.Exists(nodeModulesPath))
        {
            try
            {
                Directory.Delete(nodeModulesPath, recursive: true);
                _logger.LogInformation("Cleaned up node_modules in {Dir}", projectDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup node_modules in {Dir}", projectDir);
            }
        }
    }

    private async Task UpdatePrototypeMetadataAsync(int protoId, ClaudeCodeResult result)
    {
        await _prototypes.UpdateExecutionMetadataAsync(protoId, new PrototypeExecutionMetadata
        {
            CommandUsed = result.CommandUsed,
            WorkingDirectory = result.WorkingDirectory,
            ExitCode = result.ExitCode,
            StdoutLog = result.Stdout,
            StderrLog = result.Stderr,
            GeneratedFiles = result.GeneratedFiles != null
                ? string.Join("\n", result.GeneratedFiles)
                : null,
            BuildCommand = "npm run build",
            RunCommand = "npm run dev",
            EntryFile = result.EntryFile,
            BuildReady = result.BuildReady,
            BuildFinishedAt = result.FinishedAt ?? DateTime.UtcNow
        });
    }
}
