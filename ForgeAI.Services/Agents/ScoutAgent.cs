using ForgeAI.Core.DTOs;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Agents;

public class ScoutAgent
{
    private readonly IScoutSourceRepository _sources;
    private readonly IRawSignalRepository _signals;
    private readonly IClusterRepository _clusters;
    private readonly IOpportunityRepository _opportunities;
    private readonly IAgentRunRepository _agentRuns;
    private readonly IPipelineLogRepository _pipelineLog;
    private readonly IFocusProfileRepository _profiles;
    private readonly IBedrockService _bedrock;
    private readonly ISseBroadcaster _sse;
    private readonly IEnumerable<ISourceParser> _parsers;
    private readonly ILogger<ScoutAgent> _logger;

    public ScoutAgent(
        IScoutSourceRepository sources,
        IRawSignalRepository signals,
        IClusterRepository clusters,
        IOpportunityRepository opportunities,
        IAgentRunRepository agentRuns,
        IPipelineLogRepository pipelineLog,
        IFocusProfileRepository profiles,
        IBedrockService bedrock,
        ISseBroadcaster sse,
        IEnumerable<ISourceParser> parsers,
        ILogger<ScoutAgent> logger)
    {
        _sources = sources;
        _signals = signals;
        _clusters = clusters;
        _opportunities = opportunities;
        _agentRuns = agentRuns;
        _pipelineLog = pipelineLog;
        _profiles = profiles;
        _bedrock = bedrock;
        _sse = sse;
        _parsers = parsers;
        _logger = logger;
    }

    public async Task<ScoutRunResultDto> RunAsync(int[]? profileIds = null)
    {
        var agentRun = new AgentRun
        {
            JobType = "scout",
            Status = "running",
            StartedAt = DateTime.UtcNow
        };
        var runId = await _agentRuns.CreateAsync(agentRun);

        var result = new ScoutRunResultDto { AgentRunId = runId };

        try
        {
            var profileLabel = profileIds != null ? $"profiles: [{string.Join(",", profileIds)}]" : "all";
            _logger.LogInformation("Scout run {RunId} started ({Profiles})", runId, profileLabel);
            await _pipelineLog.LogAsync("scout", null, "run_started", $"Agent run {runId} ({profileLabel})");

            var sources = await _sources.GetEnabledSourcesAsync();

            foreach (var source in sources)
            {
                try
                {
                    var parser = _parsers.FirstOrDefault(p => p.ParserType == source.ParserType);
                    if (parser == null)
                    {
                        _logger.LogWarning("No parser found for type: {ParserType}", source.ParserType);
                        continue;
                    }

                    _logger.LogInformation("Scouting source: {Name} ({Type})", source.Name, source.ParserType);
                    var signals = await parser.FetchSignalsAsync(source);
                    result.SignalsDiscovered += signals.Count;

                    foreach (var signal in signals)
                    {
                        await _signals.CreateAsync(signal);
                    }

                    await _sources.UpdateLastRunAsync(source.Id, DateTime.UtcNow);
                    _logger.LogInformation("Source {Name}: discovered {Count} signals", source.Name, signals.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scout failed for source {SourceName}", source.Name);
                }
            }

            // Cluster unclustered signals — niche-selective when profiles are provided
            var unclustered = (await _signals.GetUnclusteredAsync()).ToList();
            if (unclustered.Count > 0)
            {
                List<FocusProfile>? selectedProfiles = null;
                if (profileIds != null && profileIds.Length > 0)
                {
                    var allProfiles = await _profiles.GetActiveAsync();
                    selectedProfiles = allProfiles.Where(p => profileIds.Contains(p.Id)).ToList();
                    _logger.LogInformation("Clustering with {Count} selected profiles: {Names}",
                        selectedProfiles.Count, string.Join(", ", selectedProfiles.Select(p => p.Name)));
                }

                var batches = unclustered.Chunk(12).ToList();
                foreach (var batch in batches)
                {
                    try
                    {
                        if (selectedProfiles != null && selectedProfiles.Count > 0)
                        {
                            // Cluster through each selected niche lens
                            foreach (var profile in selectedProfiles)
                            {
                                var clusterResults = await ClusterSignalsAsync(batch.ToList(), profile);
                                result.ClustersCreated += clusterResults.newClusters;
                                result.OpportunitiesCreated += clusterResults.newOpportunities;
                            }
                        }
                        else
                        {
                            var clusterResults = await ClusterSignalsAsync(batch.ToList());
                            result.ClustersCreated += clusterResults.newClusters;
                            result.OpportunitiesCreated += clusterResults.newOpportunities;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Clustering batch failed");
                    }
                }
            }

            result.Status = "completed";
            await _agentRuns.UpdateStatusAsync(runId, "completed");
            await _pipelineLog.LogAsync("scout", null, "run_completed",
                $"Signals: {result.SignalsDiscovered}, Clusters: {result.ClustersCreated}, Opportunities: {result.OpportunitiesCreated}");

            _logger.LogInformation("Scout run {RunId} completed. Signals: {Signals}, Clusters: {Clusters}, Opportunities: {Opps}",
                runId, result.SignalsDiscovered, result.ClustersCreated, result.OpportunitiesCreated);

            await _sse.BroadcastAsync("scout.completed", new
            {
                Signals = result.SignalsDiscovered,
                Clusters = result.ClustersCreated,
                Opportunities = result.OpportunitiesCreated
            });
        }
        catch (Exception ex)
        {
            result.Status = "failed";
            await _agentRuns.UpdateStatusAsync(runId, "failed", ex.Message);
            _logger.LogError(ex, "Scout run {RunId} failed", runId);
        }

        return result;
    }

    private async Task<(int newClusters, int newOpportunities)> ClusterSignalsAsync(List<RawSignal> signals, FocusProfile? profile = null)
    {
        int newClusters = 0;
        int newOpportunities = 0;

        var signalList = string.Join("\n", signals.Select((s, i) =>
            $"[{i + 1}] Title: {s.Title}\n    Pain point: {s.PainPoint}\n    Source: {s.Source}"));

        var nicheContext = profile != null
            ? $@"

FOCUS NICHE: {profile.DisplayName}
Target users: {profile.IncludedUsers}
Excluded users: {profile.ExcludedUsers}
Problem types to focus on: {profile.IncludedProblems}
Problem types to avoid: {profile.ExcludedProblems}
Platform bias: {profile.PlatformBias}
Example use cases: {profile.ExampleUseCases?.Replace("|", "\n  - ")}

Only create opportunities that fit this niche. Reject signals that don't match."
            : "";

        var prompt = $@"You are a use-case hunter, not an idea generator. Your job is to turn raw signals into specific, human, actionable opportunities.
{nicheContext}
Signals:
{signalList}

Group signals into clusters of the SAME core user problem. Then for each cluster, produce a concrete use case.

Return a JSON array:
[
  {{
    ""cluster_name"": ""short name for this specific problem"",
    ""summary"": ""one paragraph describing the core problem"",
    ""signal_indices"": [1, 3],
    ""opportunity_title"": ""Product Name"",
    ""plain_title"": ""What the user actually needs, in plain English"",
    ""problem_statement"": ""One sentence a non-technical person would understand"",
    ""target_audience"": ""specific user type"",
    ""user_type"": ""e.g., gym beginner, freelance marketer, solo founder"",
    ""moment_of_pain"": ""the exact moment the user feels frustrated"",
    ""bad_workaround"": ""what they do today instead (spreadsheet, manual, nothing)"",
    ""simple_use_case"": ""One sentence starting with a real user in a real moment: 'A [user] opens the app and [gets value]'"",
    ""why_now"": ""why this opportunity exists now"",
    ""platform_recommendation"": ""mobile | web | hybrid"",
    ""confidence"": 1-10
  }}
]

Rules:
- Only group signals with the SAME core problem, not just related themes
- Do NOT create broad clusters like ""Health Apps"" or ""Productivity Tools""
- Each cluster = one focused product solving one specific pain
- Every opportunity MUST have a concrete simple_use_case
- If the problem cannot be explained in one sentence to a non-technical person, reject it
- Reject generic ideas like ""platform for X"" or ""marketplace for Y"" unless the user scenario is crystal clear
- Prefer problems with a visible bad workaround
- Prefer recurring daily/weekly frustrations over interesting one-off ideas
- Single-signal clusters are fine if the problem is specific
- Return ONLY the JSON array";

        try
        {
            var response = await _bedrock.InvokeAsync(prompt);

            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < 0)
            {
                _logger.LogWarning("Clustering: no JSON array in Bedrock response");
                return (0, 0);
            }

            var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var clusters = System.Text.Json.JsonSerializer.Deserialize<List<ClusterResult>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (clusters == null) return (0, 0);

            foreach (var cluster in clusters)
            {
                var dbCluster = new OpportunityCluster
                {
                    Name = cluster.ClusterName ?? "Unnamed",
                    Summary = cluster.Summary,
                    SignalCount = cluster.SignalIndices?.Count ?? 0
                };
                var clusterId = await _clusters.CreateAsync(dbCluster);
                newClusters++;

                // Link signals to cluster
                if (cluster.SignalIndices != null)
                {
                    foreach (var idx in cluster.SignalIndices)
                    {
                        var signalIndex = idx - 1;
                        if (signalIndex >= 0 && signalIndex < signals.Count)
                        {
                            await _signals.MarkClusteredAsync(signals[signalIndex].Id, clusterId);
                        }
                    }
                }

                // Create opportunity if cluster has enough signals and no duplicate exists
                var signalThreshold = 1; // Start low, increase later
                var oppTitle = cluster.OpportunityTitle ?? cluster.ClusterName ?? "Untitled";
                var isDuplicate = await _opportunities.ExistsSimilarAsync(oppTitle);

                if ((cluster.SignalIndices?.Count ?? 0) >= signalThreshold && !isDuplicate)
                {
                    var opportunity = new Opportunity
                    {
                        ClusterId = clusterId,
                        Title = cluster.OpportunityTitle ?? cluster.ClusterName ?? "Untitled",
                        PlainTitle = cluster.PlainTitle,
                        ProblemStatement = cluster.ProblemStatement ?? cluster.Summary,
                        TargetAudience = cluster.TargetAudience,
                        WhyNow = cluster.WhyNow,
                        WhyMobileFirst = cluster.PlatformRecommendation ?? "hybrid",
                        UserType = cluster.UserType,
                        MomentOfPain = cluster.MomentOfPain,
                        BadWorkaround = cluster.BadWorkaround,
                        SimpleUseCase = cluster.SimpleUseCase,
                        FocusProfileId = profile?.Id,
                        Status = "pending_evaluation"
                    };
                    await _opportunities.CreateAsync(opportunity);
                    newOpportunities++;

                    // Check signal repetition across all runs
                    var signalStrength = await _signals.CountSimilarAsync(opportunity.ProblemStatement ?? "");
                    await _pipelineLog.LogAsync("scout", null, "opportunity_created",
                        $"'{opportunity.Title}' from cluster '{dbCluster.Name}' ({dbCluster.SignalCount} signals, {signalStrength} total similar signals)");
                }
                else if (isDuplicate)
                {
                    _logger.LogInformation("Skipping duplicate opportunity: '{Title}'", oppTitle);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clustering failed");
        }

        return (newClusters, newOpportunities);
    }

    private class ClusterResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("cluster_name")]
        public string? ClusterName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("summary")]
        public string? Summary { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("signal_indices")]
        public List<int>? SignalIndices { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("opportunity_title")]
        public string? OpportunityTitle { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("plain_title")]
        public string? PlainTitle { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("problem_statement")]
        public string? ProblemStatement { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("target_audience")]
        public string? TargetAudience { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("user_type")]
        public string? UserType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("moment_of_pain")]
        public string? MomentOfPain { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("bad_workaround")]
        public string? BadWorkaround { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("simple_use_case")]
        public string? SimpleUseCase { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("why_now")]
        public string? WhyNow { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("platform_recommendation")]
        public string? PlatformRecommendation { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public int Confidence { get; set; }
    }
}
