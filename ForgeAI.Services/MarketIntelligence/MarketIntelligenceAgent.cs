using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.MarketIntelligence;

public class MarketIntelligenceAgent
{
    private readonly ClaudeCodeMarketResearcher _researcher;
    private readonly IBedrockService _bedrock;
    private readonly IPipelineLogRepository _pipelineLog;
    private readonly ILogger<MarketIntelligenceAgent> _logger;

    public MarketIntelligenceAgent(
        ClaudeCodeMarketResearcher researcher,
        IBedrockService bedrock,
        IPipelineLogRepository pipelineLog,
        ILogger<MarketIntelligenceAgent> logger)
    {
        _researcher = researcher;
        _bedrock = bedrock;
        _pipelineLog = pipelineLog;
        _logger = logger;
    }

    public async Task<MarketReport?> AnalyzeAsync(Opportunity opportunity, CancellationToken ct = default)
    {
        _logger.LogInformation("MI: Analyzing '{Title}' via Claude Code web research", opportunity.Title);

        var report = await _researcher.ResearchAsync(opportunity, ct);

        if (report == null)
        {
            await _pipelineLog.LogAsync("market_intelligence", opportunity.Id, "research_failed",
                "Claude Code research returned no report");
            return null;
        }

        // Compress the full report into a compact summary for the evaluator
        try
        {
            report.CompactSummary = await CompressReportAsync(report, opportunity, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MI: Failed to compress report, using full version");
        }

        await _pipelineLog.LogAsync("market_intelligence", opportunity.Id, "analyzed",
            $"Competition: {report.CompetitionLevel}, Competitors: {report.CompetitorCount}, " +
            $"Maturity: {report.MarketMaturity}, Confidence: {report.Confidence:F2}, " +
            $"Complaints: {report.CommonComplaints?.Count ?? 0}, Gaps: {report.GapOpportunities?.Count ?? 0}");

        return report;
    }

    private async Task<string> CompressReportAsync(MarketReport report, Opportunity opp, CancellationToken ct)
    {
        var rawContext = report.ToFullContext();

        var prompt = $@"You are compressing a verbose market research report into a tight 150-word summary for an investor evaluator.

OPPORTUNITY: {opp.Title}

FULL RESEARCH:
{rawContext}

Write a compact summary with EXACTLY these sections:
- Competition: [level] ([maturity] market, [N] competitors)
- Dominant player: [name] — wins by [X], weakness is [Y]
- Top user complaints: [3 bullets, qualitative — no exact percentages]
- Best gap to exploit: [1-2 specific actionable opportunities]
- Verdict: [1 sentence]

RULES:
- Max 150 words total
- No exact percentages, benchmark numbers, or stats — paraphrase qualitatively
- Focus on what's actionable for building a product
- Plain language, no hype

Return only the summary text, no JSON.";

        var summary = await _bedrock.InvokeAsync(prompt, ct);
        return summary.Trim();
    }
}

public class MarketReport
{
    public int OpportunityId { get; set; }
    public string CompetitionLevel { get; set; } = "unknown";
    public int CompetitorCount { get; set; }
    public int MobileAppCount { get; set; }
    public int WebCompetitorCount { get; set; }
    public double AvgCompetitorRating { get; set; }
    public string? MarketMaturity { get; set; }
    public string? Recommendation { get; set; }
    public double Confidence { get; set; }
    public List<CompetitorApp> TopApps { get; set; } = new();
    public List<WebCompetitor>? WebCompetitors { get; set; }
    public DominantPlayer? DominantPlayer { get; set; }
    public List<string>? CommonComplaints { get; set; }
    public List<string>? GapOpportunities { get; set; }
    public string? CompactSummary { get; set; }

    // Returns the compact summary if available (preferred for evaluator), else full context
    public string ToEvaluatorContext() =>
        !string.IsNullOrEmpty(CompactSummary) ? CompactSummary : ToFullContext();

    public string ToFullContext()
    {
        var parts = new List<string>();
        parts.Add($"Competition level: {CompetitionLevel} ({CompetitorCount} competitors found, {MarketMaturity} market)");

        if (TopApps.Count > 0)
            parts.Add($"Top competitors: {string.Join(", ", TopApps.Take(3).Select(a => $"{a.Name} ({a.Downloads}, {a.Store})"))}");

        if (DominantPlayer != null && !string.IsNullOrEmpty(DominantPlayer.Name))
            parts.Add($"Dominant player: {DominantPlayer.Name} — wins because {DominantPlayer.WhyTheyWin}; weakness: {DominantPlayer.WhyTheyFail}");

        if (CommonComplaints?.Count > 0)
            parts.Add($"User complaints: {string.Join("; ", CommonComplaints)}");

        if (GapOpportunities?.Count > 0)
            parts.Add($"Gap opportunities: {string.Join("; ", GapOpportunities)}");

        if (!string.IsNullOrEmpty(Recommendation))
            parts.Add($"Researcher verdict: {Recommendation} (confidence {Confidence:F1})");

        return string.Join("\n", parts);
    }
}

public class CompetitorApp
{
    public string Name { get; set; } = "";
    public double Rating { get; set; }
    public string Downloads { get; set; } = "";
    public int ReviewCount { get; set; }
    public string Store { get; set; } = "";
    public string ProductId { get; set; } = "";
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}

public class WebCompetitor
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
}

public class DominantPlayer
{
    public string Name { get; set; } = "";
    public string WhyTheyWin { get; set; } = "";
    public string WhyTheyFail { get; set; } = "";
}
