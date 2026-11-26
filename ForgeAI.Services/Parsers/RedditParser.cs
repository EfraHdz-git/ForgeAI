using System.Net.Http.Json;
using System.Text.Json;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Parsers;

public class RedditParser : ISourceParser
{
    private readonly HttpClient _http;
    private readonly IBedrockService _bedrock;
    private readonly ILogger<RedditParser> _logger;

    private static readonly string[] DefaultSubreddits = new[]
    {
        "SideProject", "startups", "AppIdeas", "Entrepreneur", "smallbusiness"
    };

    public string ParserType => "reddit";

    public RedditParser(HttpClient http, IBedrockService bedrock, ILogger<RedditParser> logger)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ForgeAI/1.0");
        _bedrock = bedrock;
        _logger = logger;
    }

    public async Task<List<RawSignal>> FetchSignalsAsync(ScoutSource source, CancellationToken ct = default)
    {
        var signals = new List<RawSignal>();

        // Parse subreddits from source config or use defaults
        var subreddits = !string.IsNullOrEmpty(source.AuthConfig)
            ? source.AuthConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : DefaultSubreddits;

        var allPosts = new List<RedditPost>();

        foreach (var subreddit in subreddits)
        {
            try
            {
                var posts = await FetchSubredditAsync(subreddit, limit: 10, ct);
                allPosts.AddRange(posts);
                _logger.LogInformation("Reddit: fetched {Count} posts from r/{Sub}", posts.Count, subreddit);

                // Rate limit — Reddit asks for 1 request per second
                await Task.Delay(1200, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reddit: failed to fetch r/{Sub}", subreddit);
            }
        }

        _logger.LogInformation("Reddit: total {Count} posts from {Subs} subreddits, sending to Bedrock",
            allPosts.Count, subreddits.Length);

        // Batch and analyze
        var batches = allPosts.Chunk(10).ToList();
        foreach (var batch in batches)
        {
            try
            {
                var extracted = await ExtractPainPointsAsync(batch, source, ct);
                signals.AddRange(extracted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reddit: Bedrock analysis failed for batch");
            }
        }

        _logger.LogInformation("Reddit: extracted {Count} signals with pain points", signals.Count);
        return signals;
    }

    private async Task<List<RedditPost>> FetchSubredditAsync(string subreddit, int limit, CancellationToken ct)
    {
        var posts = new List<RedditPost>();

        var url = $"https://www.reddit.com/r/{subreddit}/hot.json?limit={limit}";
        var response = await _http.GetStringAsync(url, ct);
        var doc = JsonDocument.Parse(response);

        var children = doc.RootElement
            .GetProperty("data")
            .GetProperty("children");

        foreach (var child in children.EnumerateArray())
        {
            var data = child.GetProperty("data");

            // Skip stickied/pinned posts
            if (data.TryGetProperty("stickied", out var stickied) && stickied.GetBoolean())
                continue;

            var post = new RedditPost
            {
                Title = data.GetProperty("title").GetString() ?? "",
                Selftext = data.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "",
                Subreddit = subreddit,
                Score = data.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0,
                NumComments = data.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : 0,
                Url = data.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Permalink = data.TryGetProperty("permalink", out var pl) ? pl.GetString() ?? "" : ""
            };

            if (!string.IsNullOrWhiteSpace(post.Title))
                posts.Add(post);
        }

        return posts;
    }

    private async Task<List<RawSignal>> ExtractPainPointsAsync(
        RedditPost[] posts, ScoutSource source, CancellationToken ct)
    {
        var postList = string.Join("\n", posts.Select((p, i) =>
        {
            var body = p.Selftext.Length > 200 ? p.Selftext[..200] + "..." : p.Selftext;
            return $"[{i + 1}] r/{p.Subreddit} | Title: {p.Title}\n    Body: {body}\n    Score: {p.Score} | Comments: {p.NumComments}";
        }));

        var prompt = $@"You are an app opportunity scout. Analyze these Reddit posts and identify which ones reveal a real pain point, frustration, or unmet need that could be solved with a software product (mobile, web, or hybrid).

A real pain point is:
- a repeated or ongoing problem
- something users actively struggle with
- something that causes friction, inefficiency, or frustration
- NOT just a project showcase, self-promotion, or curiosity

Posts:
{postList}

For EACH post that contains a viable pain point, respond with a JSON array of objects:
[
  {{
    ""index"": 1,
    ""pain_point"": ""generalized description of the pain point"",
    ""potential_app"": ""brief idea of what app could solve this"",
    ""target_audience"": ""who would use this"",
    ""platform_hint"": ""mobile | web | hybrid"",
    ""relevance_score"": 1-10
  }}
]

Rules:
- Only include posts with genuine, recurring pain points (relevance_score >= 6)
- Skip self-promotion, launch announcements, and generic advice posts
- Reddit is full of self-promotion — be very selective
- Write pain_point in a generalized, reusable form (not tied to the specific post wording)
- Assign a platform_hint based on how the solution would most naturally be used
- If NO posts have viable pain points, return: []
- Return ONLY the JSON array, no other text";

        var response = await _bedrock.InvokeAsync(prompt, ct);

        var signals = new List<RawSignal>();
        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < 0) return signals;

            var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var items = JsonSerializer.Deserialize<List<PainPointResult>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null) return signals;

            foreach (var item in items.Where(i => i.RelevanceScore >= 6))
            {
                var postIndex = item.Index - 1;
                if (postIndex < 0 || postIndex >= posts.Length) continue;

                var post = posts[postIndex];
                signals.Add(new RawSignal
                {
                    SourceId = source.Id,
                    Source = $"reddit/r/{post.Subreddit}",
                    Title = item.PotentialApp ?? post.Title,
                    Description = $"Reddit r/{post.Subreddit}: {post.Title} (Score: {post.Score}, Comments: {post.NumComments}) [Platform: {item.PlatformHint ?? "hybrid"}]",
                    Url = $"https://www.reddit.com{post.Permalink}",
                    PainPoint = item.PainPoint,
                    FrequencyScore = item.RelevanceScore
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Reddit: failed to parse Bedrock response");
        }

        return signals;
    }

    private class RedditPost
    {
        public string Title { get; set; } = "";
        public string Selftext { get; set; } = "";
        public string Subreddit { get; set; } = "";
        public int Score { get; set; }
        public int NumComments { get; set; }
        public string Url { get; set; } = "";
        public string Permalink { get; set; } = "";
    }

    private class PainPointResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("index")]
        public int Index { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pain_point")]
        public string? PainPoint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("potential_app")]
        public string? PotentialApp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("target_audience")]
        public string? TargetAudience { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("platform_hint")]
        public string? PlatformHint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("relevance_score")]
        public int RelevanceScore { get; set; }
    }
}
