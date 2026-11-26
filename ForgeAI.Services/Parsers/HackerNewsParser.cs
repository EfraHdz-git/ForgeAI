using System.Net.Http.Json;
using System.Text.Json;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Parsers;

public class HackerNewsParser : ISourceParser
{
    private readonly HttpClient _http;
    private readonly IBedrockService _bedrock;
    private readonly ILogger<HackerNewsParser> _logger;

    public string ParserType => "hackernews";

    public HackerNewsParser(HttpClient http, IBedrockService bedrock, ILogger<HackerNewsParser> logger)
    {
        _http = http;
        _bedrock = bedrock;
        _logger = logger;
    }

    public async Task<List<RawSignal>> FetchSignalsAsync(ScoutSource source, CancellationToken ct = default)
    {
        var signals = new List<RawSignal>();

        // Fetch top stories (Show HN, Ask HN, and top stories mix)
        var storyIds = await FetchStoryIdsAsync("topstories", ct);
        var askIds = await FetchStoryIdsAsync("askstories", ct);
        var showIds = await FetchStoryIdsAsync("showstories", ct);

        // Take a sample from each to keep costs manageable
        var combined = storyIds.Take(15)
            .Concat(askIds.Take(15))
            .Concat(showIds.Take(10))
            .Distinct()
            .ToList();

        _logger.LogInformation("HN: fetching {Count} stories for analysis", combined.Count);

        // Fetch story details
        var stories = new List<HnStory>();
        foreach (var id in combined)
        {
            try
            {
                var story = await _http.GetFromJsonAsync<HnStory>(
                    $"https://hacker-news.firebaseio.com/v0/item/{id}.json", ct);
                if (story?.Title != null)
                    stories.Add(story);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HN: failed to fetch story {Id}", id);
            }
        }

        _logger.LogInformation("HN: fetched {Count} stories, sending to Bedrock for analysis", stories.Count);

        // Batch stories and ask Bedrock to find pain points
        var batches = stories.Chunk(10).ToList();
        foreach (var batch in batches)
        {
            try
            {
                var extracted = await ExtractPainPointsAsync(batch, source, ct);
                signals.AddRange(extracted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HN: Bedrock analysis failed for batch. Error: {Message}", ex.Message);
            }
        }

        _logger.LogInformation("HN: extracted {Count} signals with pain points", signals.Count);
        return signals;
    }

    private async Task<List<int>> FetchStoryIdsAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            var ids = await _http.GetFromJsonAsync<List<int>>(
                $"https://hacker-news.firebaseio.com/v0/{endpoint}.json", ct);
            return ids ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    private async Task<List<RawSignal>> ExtractPainPointsAsync(
        HnStory[] stories, ScoutSource source, CancellationToken ct)
    {
        var storyList = string.Join("\n", stories.Select((s, i) =>
            $"[{i + 1}] Title: {s.Title}\n    URL: {s.Url ?? "self-post"}\n    Type: {s.Type}\n    Score: {s.Score}\n    Comments: {s.Descendants}"));

        var prompt = $@"You are a pain point hunter, not an idea generator. Your job is to find specific, real-world frustrations that could be solved with software.

For each post, extract the PAIN — not the project, not the tech, not the concept. The pain.

A real pain point has:
- WHO: a specific type of person
- WHEN: a specific moment or context
- WHAT FAILS: what they try to do and what goes wrong
- CONSEQUENCE: why it matters (time wasted, money lost, frustration)

If you cannot identify all four, skip the post.

Posts:
{storyList}

For EACH post with a genuine pain point, respond with a JSON array:
[
  {{
    ""index"": 1,
    ""who"": ""specific user type (e.g., indie developer, busy parent, freelance marketer)"",
    ""when"": ""specific moment or context (e.g., mid-coding session, at the gym, during client call)"",
    ""what_fails"": ""what they try to do and what goes wrong"",
    ""pain_point"": ""one sentence: [who] struggles with [what] when [when], causing [consequence]"",
    ""potential_app"": ""brief product idea that solves this specific pain"",
    ""target_audience"": ""who would use this"",
    ""platform_hint"": ""mobile | web | hybrid"",
    ""opportunity_type"": ""painkiller | vitamin | trend"",
    ""relevance_score"": 1-10
  }}
]

Rules:
- Only include genuine, recurring pain points (relevance_score >= 6)
- REJECT generic ideas like ""platform for X"" or ""tool to manage Y"" unless the pain scenario is crystal clear
- REJECT posts that are just news, announcements, or entertainment
- If a post is a tool/project showcase, only extract if the UNDERLYING PROBLEM is clearly described
- Write pain_point as a complete sentence describing who + what + when + consequence
- Classify opportunity_type: painkiller = fixes strong pain, vitamin = nice-to-have, trend = riding a wave
- Strongly prefer painkillers over vitamins
- If NO posts have viable pain points, return: []
- Return ONLY the JSON array";

        var response = await _bedrock.InvokeAsync(prompt, ct);
        _logger.LogInformation("HN: Bedrock response ({Length} chars): {Preview}",
            response.Length, response.Length > 500 ? response[..500] + "..." : response);

        // Parse the JSON response
        var signals = new List<RawSignal>();
        try
        {
            // Extract JSON array from response (Bedrock might add extra text)
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < 0)
            {
                _logger.LogWarning("HN: No JSON array found in Bedrock response");
                return signals;
            }

            var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var items = JsonSerializer.Deserialize<List<PainPointResult>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null) return signals;

            _logger.LogInformation("HN: Parsed {Total} items, {Relevant} with relevance >= 6",
                items.Count, items.Count(i => i.RelevanceScore >= 6));

            foreach (var item in items.Where(i => i.RelevanceScore >= 6))
            {
                var storyIndex = item.Index - 1;
                if (storyIndex < 0 || storyIndex >= stories.Length) continue;

                var story = stories[storyIndex];
                signals.Add(new RawSignal
                {
                    SourceId = source.Id,
                    Source = "hackernews",
                    Title = $"{item.PotentialApp ?? story.Title}",
                    Description = $"HN: {story.Title} (Score: {story.Score}, Comments: {story.Descendants}) [Platform: {item.PlatformHint ?? "hybrid"}] [Type: {item.OpportunityType ?? "unknown"}] [Who: {item.Who}] [When: {item.When}]",
                    Url = story.Url ?? $"https://news.ycombinator.com/item?id={story.Id}",
                    PainPoint = item.PainPoint,
                    FrequencyScore = item.RelevanceScore
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "HN: failed to parse Bedrock response as JSON");
        }

        return signals;
    }

    private class HnStory
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Type { get; set; }
        public int Score { get; set; }
        public int Descendants { get; set; }
    }

    private class PainPointResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("index")]
        public int Index { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("who")]
        public string? Who { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("when")]
        public string? When { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("what_fails")]
        public string? WhatFails { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pain_point")]
        public string? PainPoint { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("potential_app")]
        public string? PotentialApp { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("target_audience")]
        public string? TargetAudience { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("platform_hint")]
        public string? PlatformHint { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("opportunity_type")]
        public string? OpportunityType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("relevance_score")]
        public int RelevanceScore { get; set; }
    }
}
