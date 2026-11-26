using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.MarketIntelligence;

public class ClaudeCodeMarketResearcher
{
    private readonly ILogger<ClaudeCodeMarketResearcher> _logger;
    private readonly string _researchBasePath;
    private readonly int _timeoutMinutes;

    public ClaudeCodeMarketResearcher(IConfiguration config, ILogger<ClaudeCodeMarketResearcher> logger)
    {
        _logger = logger;
        _researchBasePath = config["Pipeline:ResearchBasePath"]
            ?? Path.Combine(config["Pipeline:PrototypesBasePath"] ?? "/tmp", "..", "research");
        _timeoutMinutes = config.GetValue<int>("Pipeline:ResearchTimeoutMinutes", 8);
    }

    public async Task<MarketReport?> ResearchAsync(Opportunity opportunity, CancellationToken ct = default)
    {
        var researchDir = Path.GetFullPath(Path.Combine(_researchBasePath, $"research-{opportunity.Id}"));
        Directory.CreateDirectory(researchDir);

        var reportPath = Path.Combine(researchDir, "market-report.json");
        if (File.Exists(reportPath)) File.Delete(reportPath);

        var prompt = BuildPrompt(opportunity);

        _logger.LogInformation("MI: Starting Claude Code research for '{Title}' in {Dir}",
            opportunity.Title, researchDir);

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "-p --dangerously-skip-permissions",
                WorkingDirectory = researchDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.StandardInput.WriteAsync(prompt);
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(_timeoutMinutes));

            try { await process.WaitForExitAsync(timeoutCts.Token); }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("MI: Research timed out for '{Title}'", opportunity.Title);
                try { process.Kill(true); } catch { }
                return null;
            }

            if (!File.Exists(reportPath))
            {
                _logger.LogWarning("MI: market-report.json not written for '{Title}'. Exit: {Exit}",
                    opportunity.Title, process.ExitCode);
                return null;
            }

            var json = await File.ReadAllTextAsync(reportPath, ct);
            var report = ParseReport(json, opportunity.Id);

            _logger.LogInformation(
                "MI: Research done for '{Title}' — {CompCount} competitors, {CompLevel}, confidence {Conf}",
                opportunity.Title, report?.CompetitorCount ?? 0, report?.CompetitionLevel, report?.Confidence);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MI: Research failed for '{Title}'", opportunity.Title);
            return null;
        }
    }

    private string BuildPrompt(Opportunity opp) => $@"You are a market research analyst. Your job is to find REAL competitors for a specific product idea using web search and return structured findings.

OPPORTUNITY:
Title: {opp.Title}
Problem: {opp.ProblemStatement}
Target user: {opp.UserType ?? opp.TargetAudience}
Use case: {opp.SimpleUseCase ?? opp.ProblemStatement}
Platform: {opp.WhyMobileFirst ?? "hybrid"}

TASK:
1. Perform 2-3 targeted web searches using the problem and use case.
2. Identify 3-5 REAL competitors (web apps, SaaS, or mobile apps).
3. For each competitor:
   - Visit their website
   - Extract pricing, positioning, and key features
4. Find REAL user feedback (only if visible in results) from:
   - Reddit OR
   - Product Hunt OR
   - G2 / Trustpilot
   (Do NOT fabricate feedback if none is found)
5. Identify:
   - repeated weaknesses
   - clear gaps in current solutions

CONSTRAINTS:
- Max 3-5 competitors
- Max 3 searches
- Do NOT guess or fabricate data
- If data is missing, write ""unknown""
- Prioritize accuracy over completeness
- Prefer niche, specialized tools over generic large platforms (e.g., prefer ""AI meeting summary SaaS for lawyers"" over ""Notion"")

COMPETITOR SCOPE:
When the use case involves building, generating, or producing outputs (prototypes, apps, plans, documents, designs, etc.):
- Include EXECUTION tools: AI app builders, no-code prototype generators, developer copilots, design-to-code tools
- Include WORKFLOW tools: platforms that span multiple steps (research → build → deploy)
- Not just ""validators"" or analysis tools — compare against the tools users would actually pick instead to ship something
- Example: for an idea→prototype platform, competitors include Lovable, Bolt, v0, Replit Agent — not just idea-validation tools

DIRECT vs ADJACENT competitors:
For each competitor, classify relationship in the ""type"" field as:
- ""direct"" = same workflow AND same problem (users choose one OR the other)
- ""adjacent"" = infrastructure, partial overlap, or tangentially related (users might combine with the product, not replace)
If all found competitors are adjacent, explicitly note in the recommendation that the product may be creating a NEW category rather than entering an existing one.

OUTPUT:
Write a file called `market-report.json` with EXACTLY this structure:

{{
  ""research_date"": ""ISO timestamp"",
  ""keywords_used"": [""query1"", ""query2""],
  ""competitors"": [
    {{
      ""name"": ""Product name"",
      ""url"": ""https://..."",
      ""type"": ""web | mobile | hybrid"",
      ""pricing"": ""free | freemium | paid | unknown"",
      ""description"": ""One-line summary of what this product does and who it targets"",
      ""strengths"": [""specific strength"", ""specific strength""],
      ""weaknesses"": [""specific weakness"", ""specific weakness""]
    }}
  ],
  ""dominant_player"": {{
    ""name"": ""Top competitor"",
    ""why_they_win"": ""specific advantage"",
    ""why_they_fail"": ""specific limitation""
  }},
  ""common_complaints"": [
    ""evidence-based complaint"",
    ""evidence-based complaint""
  ],
  ""gap_opportunities"": [
    ""specific unmet need"",
    ""clear product gap""
  ],
  ""market_maturity"": ""nascent | growing | saturated"",
  ""competition_level"": ""none | low | medium | high"",
  ""competitor_count_found"": 0,
  ""revenue_projection"": {{
    ""pricing_benchmark"": ""$X-$Y/month based on competitors"",
    ""early_users_estimate"": ""e.g., 50 users × $X/month = $Y/month realistic early revenue"",
    ""scaled_users_estimate"": ""e.g., 1000 users × $X/month = $Y/month at 12-month maturity"",
    ""notes"": ""assumptions and caveats""
  }},
  ""uniqueness_level"": ""high_novelty | moderate | low"",
  ""category_name"": ""Short name of the category this product occupies or creates (e.g. 'AI Agent Supervision Layer', 'Idea-to-App Execution Platform'). Leave empty if the product is a conventional entry into an existing well-defined category."",
  ""category_position"": ""One-sentence description of where this sits in the stack / market (e.g. 'Middleware between AI agent execution and human oversight'). Leave empty if not applicable."",
  ""category_why_now"": ""One-sentence explanation of what recent shift makes this category needed now. Leave empty if not applicable."",
  ""market_timing"": ""Why now is the right time for this idea — specific trend, regulation, or shift"",
  ""next_steps"": [
    ""Specific actionable step 1 (e.g., Build MVP focused only on X feature)"",
    ""Specific actionable step 2 (e.g., Target Y users first, ignore Z)"",
    ""Specific actionable step 3 (e.g., Validate with 5-10 users before expanding)"",
    ""Specific actionable step 4 (e.g., Price at $X/month based on competitor benchmarks)""
  ],
  ""biggest_risks"": [
    ""Specific risk 1 (e.g., Users may not want another standalone app)"",
    ""Specific risk 2 (e.g., Regulatory concerns could slow adoption)"",
    ""Specific risk 3 (e.g., Requires behavior change that's hard to sustain)""
  ],
  ""brutal_honesty"": ""If this idea fails, it will be because: [specific likely failure mode — not generic]"",
  ""recommendation"": ""clear 1-sentence verdict"",
  ""confidence"": 0.0
}}

RULES:
- Weaknesses must be SPECIFIC (e.g., ""no offline mode"", not ""bad UX"")
- Gaps must be actionable (what exactly is missing)
- Do NOT repeat generic phrases like ""better UI"" or ""more features""
- If no competitors found, explicitly say why in recommendation
- Confidence should reflect certainty (0.3 = weak, 0.8 = strong)
- If you didn't find real user feedback, leave common_complaints as an empty array
- next_steps must be ACTIONABLE instructions (""Build X"", ""Target Y"", ""Price at $Z"") — not observations
- For multi-step platforms, next_steps should reinforce the FULL workflow, not recommend dropping core steps
- biggest_risks must be SPECIFIC failure modes tied to THIS product — not generic. BAD: ""users may prefer specialized tools."" GOOD: ""Prototype output quality may not be high enough to validate ideas meaningfully"" or ""Users may not trust AI-generated market research without methodology transparency""
- If competitors solve adjacent steps but none connect them, explicitly state this in the recommendation field: ""Validation tools don't execute; builders don't validate — this closes the gap""
- The recommendation should be ONE dominant transformation statement (e.g., ""From idea to testable product in one flow""), not a feature list. Pick the strongest narrative and lead with it.

CATEGORY FIELDS — when to populate:
Only populate category_name, category_position, and category_why_now when ONE of these is true:
  - No direct competitors exist (all found competitors are adjacent)
  - The product clearly creates or defines a new category in the stack
  - There is a structural gap between existing categories that this product bridges
If the product is a conventional entrant in an established category, leave these fields as empty strings. Do NOT force a category label for commodity products.
- uniqueness_level: ""high_novelty"" = no direct competitor does this, ""moderate"" = exists but with clear gaps, ""low"" = crowded space
- market_timing: explain WHY NOW — reference specific trends, regulatory changes, or technology shifts
- Each competitor ""description"" must be a single clear sentence explaining what the product does

EVIDENCE RULES (very important):
- Do NOT state exact percentages, benchmark scores, or statistics as facts
- Instead, use qualitative framing:
  - BAD: ""Scored 1/5 on completeness and 2/5 on depth in independent benchmarks""
  - GOOD: ""Reported as shallow analysis depth in third-party benchmark claims""
  - BAD: ""46% of developers distrust AI code review""
  - GOOD: ""Developer distrust of AI review accuracy is a commonly raised concern""
- If you cite a specific stat, prefix it with ""source-reported:"" to signal it's from marketing/third-party, not verified truth
- Do NOT invent stats. If you don't find a real number, describe qualitatively.

DO NOT output anything except writing market-report.json. Do NOT build code. Do NOT ask questions.";

    private MarketReport? ParseReport(string json, int opportunityId)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var report = new MarketReport
            {
                OpportunityId = opportunityId,
                CompetitionLevel = root.GetStringOrDefault("competition_level", "unknown"),
                CompetitorCount = root.GetIntOrDefault("competitor_count_found"),
                MarketMaturity = root.GetStringOrDefault("market_maturity", "unknown"),
                Recommendation = root.GetStringOrDefault("recommendation"),
                Confidence = root.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number
                    ? conf.GetDouble() : 0.0
            };

            if (root.TryGetProperty("competitors", out var comps))
            {
                foreach (var c in comps.EnumerateArray().Take(5))
                {
                    report.TopApps.Add(new CompetitorApp
                    {
                        Name = c.GetStringOrDefault("name"),
                        Store = c.GetStringOrDefault("type"),
                        ProductId = c.GetStringOrDefault("url"),
                        Downloads = c.GetStringOrDefault("pricing"),
                        Strengths = c.TryGetProperty("strengths", out var s) && s.ValueKind == JsonValueKind.Array
                            ? s.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>(),
                        Weaknesses = c.TryGetProperty("weaknesses", out var w) && w.ValueKind == JsonValueKind.Array
                            ? w.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>()
                    });
                }
            }

            if (root.TryGetProperty("dominant_player", out var dom) && dom.ValueKind == JsonValueKind.Object)
            {
                report.DominantPlayer = new DominantPlayer
                {
                    Name = dom.GetStringOrDefault("name"),
                    WhyTheyWin = dom.GetStringOrDefault("why_they_win"),
                    WhyTheyFail = dom.GetStringOrDefault("why_they_fail")
                };
            }

            if (root.TryGetProperty("common_complaints", out var cc) && cc.ValueKind == JsonValueKind.Array)
                report.CommonComplaints = cc.EnumerateArray().Select(x => x.GetString() ?? "").ToList();

            if (root.TryGetProperty("gap_opportunities", out var go) && go.ValueKind == JsonValueKind.Array)
                report.GapOpportunities = go.EnumerateArray().Select(x => x.GetString() ?? "").ToList();

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MI: Failed to parse market-report.json");
            return null;
        }
    }
}

static class JsonExt
{
    public static string GetStringOrDefault(this JsonElement e, string key, string def = "")
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;

    public static int GetIntOrDefault(this JsonElement e, string key, int def = 0)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
}
