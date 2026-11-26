using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using ForgeAI.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Agents;

public class EvaluatorAgent
{
    private readonly IOpportunityRepository _opportunities;
    private readonly IEvaluationRepository _evaluations;
    private readonly IAgentRunRepository _agentRuns;
    private readonly IPipelineLogRepository _pipelineLog;
    private readonly IBedrockService _bedrock;
    private readonly EmailService _email;
    private readonly ISseBroadcaster _sse;
    private readonly MarketIntelligence.MarketIntelligenceAgent _marketIntel;
    private readonly ILogger<EvaluatorAgent> _logger;
    private readonly int _rejectBelow;
    private readonly int _prototypeAbove;

    public EvaluatorAgent(
        IOpportunityRepository opportunities,
        IEvaluationRepository evaluations,
        IAgentRunRepository agentRuns,
        IPipelineLogRepository pipelineLog,
        IBedrockService bedrock,
        EmailService email,
        ISseBroadcaster sse,
        MarketIntelligence.MarketIntelligenceAgent marketIntel,
        IConfiguration config,
        ILogger<EvaluatorAgent> logger)
    {
        _opportunities = opportunities;
        _evaluations = evaluations;
        _agentRuns = agentRuns;
        _pipelineLog = pipelineLog;
        _bedrock = bedrock;
        _email = email;
        _sse = sse;
        _marketIntel = marketIntel;
        _logger = logger;
        _rejectBelow = config.GetValue<int>("Pipeline:EvaluationThresholds:RejectBelow", 40);
        _prototypeAbove = config.GetValue<int>("Pipeline:EvaluationThresholds:PrototypeAbove", 70);
    }

    /// <summary>
    /// Single-opportunity evaluation for public submissions.
    /// Runs initial eval → market research (if score >= threshold) → final eval → report ready.
    /// Broadcasts SSE stage events so the Processing page can show progress.
    /// </summary>
    public async Task EvaluateSingleAsync(int opportunityId, int marketResearchThreshold = 40, bool alwaysRunMarketResearch = false)
    {
        var opp = await _opportunities.GetByIdAsync(opportunityId);
        if (opp == null) return;

        _logger.LogInformation("Single evaluation started for '{Title}' (id={Id})", opp.Title, opp.Id);

        // Give frontend time to establish SSE connection before broadcasting
        await Task.Delay(2000);

        // Stage 1: Initial evaluation
        await BroadcastStage(opportunityId, "evaluating");
        var evaluation = await EvaluateOpportunityAsync(opp);
        var score = evaluation.OverallScore ?? 0;
        var enriched = false;

        _logger.LogInformation("Initial eval for '{Title}': {Score}/100", opp.Title, score);

        // Stage 2: Market research — always for public submissions, threshold for batch
        if (alwaysRunMarketResearch || score >= marketResearchThreshold)
        {
            await BroadcastStage(opportunityId, "researching_market");
            try
            {
                var marketReport = await _marketIntel.AnalyzeAsync(opp);
                if (marketReport != null)
                {
                    // Stage 3: Re-evaluate with market data
                    await BroadcastStage(opportunityId, "finalizing");
                    evaluation = await EvaluateOpportunityAsync(opp, marketReport.ToEvaluatorContext());
                    enriched = true;
                    _logger.LogInformation("Re-evaluated '{Title}' with market data: {Score}/100",
                        opp.Title, evaluation.OverallScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MI failed for '{Title}', using initial evaluation", opp.Title);
            }
        }

        if (!enriched)
            await BroadcastStage(opportunityId, "finalizing");

        // Save evaluation and transition status
        await _evaluations.CreateAsync(evaluation);
        var newStatus = DetermineStatus(evaluation);
        await _opportunities.UpdateStatusAsync(opportunityId, newStatus);
        await LogEvaluation(opp, evaluation, enriched);

        await _sse.BroadcastAsync("opportunity.evaluated", new
        {
            OpportunityId = opp.Id,
            Title = opp.Title,
            Score = evaluation.OverallScore,
            Recommendation = evaluation.Recommendation,
            Status = newStatus
        });

        // Stage 4: Report ready
        await BroadcastStage(opportunityId, "report_ready");
        _logger.LogInformation("Single evaluation complete for '{Title}': {Score}/100 → {Status}",
            opp.Title, evaluation.OverallScore, newStatus);
    }

    /// <summary>
    /// Batch evaluation for all pending opportunities (called by orchestrator).
    /// Runs initial eval on all, shortlists top 5 for deep research.
    /// </summary>
    public async Task<int> RunAsync()
    {
        var agentRun = new AgentRun
        {
            JobType = "evaluator",
            Status = "running",
            StartedAt = DateTime.UtcNow
        };
        var runId = await _agentRuns.CreateAsync(agentRun);

        try
        {
            var pending = await _opportunities.GetByStatusAsync("pending_evaluation");
            int evaluated = 0;
            var digestItems = new List<EmailService.DigestItem>();

            // Stage 1: Initial evaluation on ALL (cheap, fast via Haiku)
            var initialEvals = new List<(Opportunity opp, Evaluation eval, int clarity)>();
            foreach (var opportunity in pending)
            {
                try
                {
                    var evaluation = await EvaluateOpportunityAsync(opportunity);
                    var clarity = ExtractClarityScore(evaluation.Reasoning);
                    initialEvals.Add((opportunity, evaluation, clarity));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Initial evaluation failed for {Id}: {Title}", opportunity.Id, opportunity.Title);
                    await _pipelineLog.LogAsync("evaluator", opportunity.Id, "initial_eval_failed", ex.Message);
                }
            }

            // Stage 2: Shortlist top 5 for deep research
            var shortlist = initialEvals
                .Where(e => (e.eval.OverallScore ?? 0) >= 60 && e.clarity >= 70)
                .OrderByDescending(e => (e.eval.Summary ?? "").Contains("painkiller", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(e => e.clarity)
                .ThenByDescending(e => e.eval.OverallScore)
                .Take(5)
                .Select(e => e.opp.Id)
                .ToHashSet();

            _logger.LogInformation("Evaluator shortlist: {Count} ideas qualify for deep research", shortlist.Count);

            // Stage 3: Process each with research if shortlisted
            foreach (var (opportunity, initialEval, clarityScore) in initialEvals)
            {
                try
                {
                    var evaluation = initialEval;
                    var enriched = false;

                    if (shortlist.Contains(opportunity.Id))
                    {
                        try
                        {
                            var marketReport = await _marketIntel.AnalyzeAsync(opportunity);
                            if (marketReport != null)
                            {
                                _logger.LogInformation("Re-evaluating '{Title}' with market data", opportunity.Title);
                                evaluation = await EvaluateOpportunityAsync(opportunity, marketReport.ToEvaluatorContext());
                                enriched = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Market intelligence failed for '{Title}', using initial evaluation", opportunity.Title);
                        }
                    }

                    await _evaluations.CreateAsync(evaluation);
                    var newStatus = DetermineStatus(evaluation);
                    await _opportunities.UpdateStatusAsync(opportunity.Id, newStatus);
                    await LogEvaluation(opportunity, evaluation, enriched);

                    await _sse.BroadcastAsync("opportunity.evaluated", new
                    {
                        OpportunityId = opportunity.Id,
                        Title = opportunity.Title,
                        Score = evaluation.OverallScore,
                        Recommendation = evaluation.Recommendation,
                        Status = newStatus
                    });

                    if (newStatus == "pending_approval")
                    {
                        var type = ExtractType(evaluation.Summary);
                        digestItems.Add(new EmailService.DigestItem(
                            opportunity.Id,
                            opportunity.PlainTitle ?? opportunity.Title,
                            evaluation.OverallScore ?? 0,
                            type,
                            opportunity.SimpleUseCase));
                    }

                    evaluated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Evaluation failed for opportunity {Id}: {Title}",
                        opportunity.Id, opportunity.Title);
                    await _pipelineLog.LogAsync("evaluator", opportunity.Id, "evaluation_failed", ex.Message);
                }
            }

            await _agentRuns.UpdateStatusAsync(runId, "completed");
            _logger.LogInformation("Evaluator run {RunId} completed. Evaluated: {Count}, Need approval: {Digest}",
                runId, evaluated, digestItems.Count);

            if (digestItems.Count > 0)
            {
                await _email.SendDigestAsync(digestItems);
            }

            return evaluated;
        }
        catch (Exception ex)
        {
            await _agentRuns.UpdateStatusAsync(runId, "failed", ex.Message);
            _logger.LogError(ex, "Evaluator run {RunId} failed", runId);
            throw;
        }
    }

    private string DetermineStatus(Evaluation evaluation)
    {
        var protoReady = evaluation.Reasoning?.Contains("[proto_ready:true]") ?? false;
        return evaluation.Recommendation switch
        {
            "prototype" when protoReady => "pending_approval",
            "prototype" => "watching",
            "watch" => "watching",
            _ => "rejected"
        };
    }

    private async Task LogEvaluation(Opportunity opp, Evaluation evaluation, bool enriched)
    {
        await _pipelineLog.LogAsync("evaluator", opp.Id, "evaluated",
            $"Score: {evaluation.OverallScore}/100, Rec: {evaluation.Recommendation} | " +
            $"Market:{evaluation.MarketFitScore} Platform:{evaluation.MobileFitScore} " +
            $"Complexity:{evaluation.ComplexityScore} Competition:{evaluation.CompetitionScore} " +
            $"Monetization:{evaluation.MonetizationScore} {(enriched ? "[market-enriched]" : "")}");

        _logger.LogInformation(
            "Evaluated '{Title}': {Score}/100 → {Rec} {Enriched}",
            opp.Title, evaluation.OverallScore, evaluation.Recommendation,
            enriched ? "[market-enriched]" : "");
    }

    private async Task BroadcastStage(int opportunityId, string stage)
    {
        await _sse.BroadcastAsync("opportunity.stage", new
        {
            OpportunityId = opportunityId,
            Stage = stage
        });
    }

    private static string? ExtractType(string? summary)
    {
        if (string.IsNullOrEmpty(summary)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(summary, @"^\[(painkiller|vitamin|trend|unknown)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToLower() : null;
    }

    private int ExtractClarityScore(string? reasoning)
    {
        if (string.IsNullOrEmpty(reasoning)) return 0;
        // Try to extract clarity score from the [Confidence: X/10] prefix we add
        var idx = reasoning.IndexOf("Confidence:");
        if (idx >= 0)
        {
            var numStr = reasoning.Substring(idx + 11).TrimStart().Split('/')[0].Trim();
            if (int.TryParse(numStr, out var conf)) return conf * 10; // scale 1-10 to 10-100
        }
        return 50; // default
    }

    private async Task<Evaluation> EvaluateOpportunityAsync(Opportunity opportunity, string? marketContext = null)
    {
        var useCaseContext = "";
        if (!string.IsNullOrEmpty(opportunity.SimpleUseCase))
            useCaseContext = $@"
Use case: {opportunity.SimpleUseCase}
User type: {opportunity.UserType}
Moment of pain: {opportunity.MomentOfPain}
Current bad workaround: {opportunity.BadWorkaround}";

        var prompt = $@"You are a skeptical venture investor evaluating new product opportunities.
Your goal is to reject weak ideas and only approve high-potential opportunities.

OPPORTUNITY:
Title: {opportunity.Title}
Plain title: {opportunity.PlainTitle ?? opportunity.Title}
Problem: {opportunity.ProblemStatement}
Target audience: {opportunity.TargetAudience}
Why now: {opportunity.WhyNow}
Platform recommendation: {opportunity.WhyMobileFirst}
{useCaseContext}

Before scoring, ask yourself:
""Would I personally invest time or money in building this?""
{(marketContext != null ? $@"
MARKET INTELLIGENCE DATA:
{marketContext}

Use this data to:
- Validate or challenge the competition score
- Identify specific gaps that make this opportunity unique
- Reference real user complaints if available
- Explain why existing solutions fail
" : "")}

SCORING DIMENSIONS:

market_fit (0-100):
  0-30: weak or unclear problem
  31-60: real but niche or low urgency
  61-80: strong recurring pain
  81-100: urgent, widespread problem
  Consider frequency of use — daily/weekly problems score higher.

platform_fit (0-100):
  How well does the proposed platform (mobile/web/hybrid) match the problem?
  0-30: poor fit
  31-60: usable but not ideal
  61-80: strong fit
  81-100: critical to the experience

complexity (0-100):
  How simple is it to build a v1? Higher = simpler.
  0-30: very complex, heavy infrastructure
  31-60: moderate complexity
  61-80: straightforward MVP
  81-100: extremely simple to build

competition (0-100):
  Higher = less competition.
  0-30: saturated market
  31-60: competitive but room exists
  61-80: relatively open
  81-100: very few direct competitors

monetization (0-100):
  0-30: unclear or weak
  31-60: possible but uncertain
  61-80: clear path
  81-100: strong and proven model

clarity_score (0-100):
  Can this idea be explained in one sentence to a non-technical person?
  Is there a single obvious first user?
  Is there a clear first feature that demonstrates value?
  0-30: vague, abstract, hard to explain
  31-60: somewhat understandable, still broad
  61-80: clear user and use case
  81-100: instantly understandable and compelling
  IMPORTANT: if clarity_score < 60, cap overall_score at 59 (no prototype)

Also provide:
- overall_score: weighted average (market_fit 35%, platform_fit 20%, complexity 20%, monetization 15%, competition 10%) — but capped at 59 if clarity_score < 60
- summary: 2-3 sentence executive summary written in plain English
- reasoning: must include:
    - a specific user scenario (who feels this pain and when)
    - why existing solutions fail
    - key strengths and key risks
    - any fatal flaws
    - whether this idea is too broad or too abstract
    - what makes this easy (or hard) to explain
- confidence: 1-10 (how certain you are about this evaluation)
- recommendation: ""reject"" if overall < {_rejectBelow}, ""watch"" if {_rejectBelow}-{_prototypeAbove - 1}, ""prototype"" if >= {_prototypeAbove}

Rules:
- Be conservative — only high-quality opportunities should reach ""prototype""
- Penalize ideas with low frequency usage or weak differentiation
- Penalize broad ideas like ""platform for X"" or ""marketplace for Y""
- Do NOT inflate scores
- Write reasoning in plain English, mentioning a specific user scenario
- If you cannot explain the idea in one sentence, it should not score above 59

EXECUTION PLATFORM RULE (important):
If the proposed solution includes any of these:
  - generating working prototypes or apps
  - creating business plans or strategic outputs
  - iterative improvements (v2, v3, refinement loops)
  - end-to-end workflow (idea → validation → build → plan)

Then treat this as an execution platform, NOT a validation-only tool:
- The differentiator IS the full loop. Do NOT recommend removing prototype generation, plans, or iteration to ""simplify the MVP.""
- MVP definition for multi-step platforms: the smallest version of the FULL workflow (idea → report → prototype → iteration), where each step is simplified but the flow must exist end-to-end. Do NOT reduce MVP to a single step.
- Prioritize showing end-to-end value over reducing scope to a single feature.

INTEGRATION AS A STRENGTH:
Do not penalize multi-step platforms for complexity if:
  - the workflow is coherent (each step reinforces the next)
  - existing competitors solve adjacent steps but none connect them
  - integration simplifies the user's experience vs. stitching multiple tools

In these cases, treat integration as a competitive moat, not a complexity problem. The complexity score should reflect BUILD difficulty, not user-facing workflow depth.

STRUCTURAL GAP INSIGHT:
When existing competitors solve adjacent steps of a workflow but none connect them into a single integrated product, explicitly call this out in the reasoning. This is usually the strongest opportunity framing — e.g., ""validation tools don't produce action; builders don't validate ideas; this closes the gap.""

SCORING CALIBRATION FOR GAP-CLOSING PLATFORMS:
If ALL of these are true:
  - a clear workflow gap exists (users currently stitch multiple tools together)
  - competitors are fragmented across adjacent steps
  - the solution unifies them into a coherent single workflow
Then bias overall_score upward into the 65-78 range unless there are major unresolved risks (e.g., fatal technical challenge, regulated market, or no clear willingness to pay). Do not settle at ""medium"" (50-65) for a product that demonstrably closes an industry-wide fragmentation gap.

POSITIONING LANGUAGE:
When writing summary and reasoning:
- Prioritize ONE dominant positioning statement framed as a transformation, not a feature list (e.g., ""From idea to testable product in one flow"" — not ""A platform with validation, prototyping, and business plans"")
- Avoid listing multiple equal-weight strengths; pick the strongest narrative and lead with it
- Express the product as what it DOES FOR the user, not what it CONTAINS

TIME-TO-VALUE LENS:
When relevant, explicitly evaluate and highlight time-to-value as a competitive dimension:
  - How quickly does a user get from raw idea to something testable/shareable?
  - How does this compare to the fragmented status quo (manual research + separate builder + separate plan doc)?
If the solution dramatically compresses this loop (e.g., minutes instead of weeks), call it out as a primary advantage, not a secondary feature.

CATEGORY CREATION:
If no direct competitor exists (tools that solve the exact same workflow), the product may be creating a NEW category rather than entering an existing one. When this applies:
  - Explicitly name the new category in the reasoning (e.g., ""AI Agent Supervision Layer"", ""Idea-to-App Execution Platform"")
  - Describe the category in one sentence: what position does this occupy in the stack?
  - Don't treat adjacent tools as direct competitors — name them as adjacent and explain what's structurally different

EXPANSION POTENTIAL:
Evaluate if the solution has strategic upside beyond its initial wedge:
  - Can it evolve from a point tool into infrastructure or a platform layer?
  - Are there natural expansion vectors (multi-tenant, enterprise governance, audit/compliance, orchestration across multiple instances)?
If yes, call this out as strategic upside in the reasoning. Don't overweight it in scoring (the MVP is what matters now), but surface it so the founder sees the long-term play.

SPECIFIC RISKS ONLY:
In reasoning and risk framing, avoid generic risks like ""users may prefer specialized tools"" or ""technical users will pick competitor X."" Replace with specific, actionable failure modes tied to THIS product, e.g.:
  - ""Prototype quality not good enough to meaningfully validate ideas""
  - ""Users don't trust AI-generated validation without seeing methodology""
  - ""Time-to-first-value too slow vs. instant-response alternatives""

AVOID GENERIC ADVICE:
Do not suggest ""focus only on validation,"" ""remove the prototype feature,"" or ""start with just one small piece"" when the core differentiator is the full workflow. Instead, identify the core differentiator and recommend a simpler version of THAT differentiator.

Return ONLY a JSON object:
{{
  ""market_fit"": 75,
  ""platform_fit"": 80,
  ""complexity"": 60,
  ""competition"": 55,
  ""monetization"": 70,
  ""clarity_score"": 72,
  ""overall_score"": 69,
  ""opportunity_type"": ""painkiller | vitamin | trend"",
  ""core_use_case"": ""One sentence: A [user] does [action] and gets [value]"",
  ""target_user"": ""specific persona"",
  ""trigger_moment"": ""when they would open this app"",
  ""why_current_tools_fail"": ""what's wrong with existing solutions"",
  ""summary"": ""..."",
  ""reasoning"": ""..."",
  ""confidence"": 7,
  ""recommendation"": ""watch"",
  ""prototype_ready"": true
}}

Prototype readiness check — set prototype_ready to true ONLY if ALL of these are true:
- clear use case (not vague)
- clear target user (not ""everyone"")
- clear gap from competitors (not just ""better UX"")
- simple MVP definition (1-2 core features)
If any are missing, set prototype_ready to false even if score >= 70.";

        var response = await _bedrock.InvokeAsync(prompt);

        // Extract JSON from response
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < 0)
            throw new InvalidOperationException("No JSON object in Bedrock evaluation response");

        var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
        var result = JsonSerializer.Deserialize<EvaluationResult>(json)
            ?? throw new InvalidOperationException("Failed to deserialize evaluation result");

        return new Evaluation
        {
            OpportunityId = opportunity.Id,
            MarketFitScore = result.MarketFit,
            ComplexityScore = result.Complexity,
            CompetitionScore = result.Competition,
            MonetizationScore = result.Monetization,
            MobileFitScore = result.PlatformFit,
            OverallScore = result.OverallScore,
            Summary = $"[{result.OpportunityType ?? "unknown"}] {result.Summary}",
            Recommendation = result.Recommendation ?? "watch",
            Reasoning = $"[Confidence: {result.Confidence}/10] [proto_ready:{result.PrototypeReady.ToString().ToLower()}] " +
                $"Use case: {result.CoreUseCase} | User: {result.TargetUser} | Trigger: {result.TriggerMoment} | " +
                $"Why current tools fail: {result.WhyCurrentToolsFail} | {result.Reasoning}",
            ClarityScore = result.ClarityScore
        };
    }

    private class EvaluationResult
    {
        [JsonPropertyName("market_fit")]
        public int MarketFit { get; set; }

        [JsonPropertyName("platform_fit")]
        public int PlatformFit { get; set; }

        [JsonPropertyName("complexity")]
        public int Complexity { get; set; }

        [JsonPropertyName("competition")]
        public int Competition { get; set; }

        [JsonPropertyName("monetization")]
        public int Monetization { get; set; }

        [JsonPropertyName("clarity_score")]
        public int ClarityScore { get; set; }

        [JsonPropertyName("overall_score")]
        public int OverallScore { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [JsonPropertyName("confidence")]
        public int Confidence { get; set; }

        [JsonPropertyName("recommendation")]
        public string? Recommendation { get; set; }

        [JsonPropertyName("opportunity_type")]
        public string? OpportunityType { get; set; }

        [JsonPropertyName("core_use_case")]
        public string? CoreUseCase { get; set; }

        [JsonPropertyName("target_user")]
        public string? TargetUser { get; set; }

        [JsonPropertyName("trigger_moment")]
        public string? TriggerMoment { get; set; }

        [JsonPropertyName("why_current_tools_fail")]
        public string? WhyCurrentToolsFail { get; set; }

        [JsonPropertyName("prototype_ready")]
        public bool PrototypeReady { get; set; }
    }
}
