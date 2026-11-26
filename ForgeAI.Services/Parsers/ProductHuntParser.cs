using System.Text;
using System.Text.Json;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Parsers;

public class ProductHuntParser : ISourceParser
{
    private readonly HttpClient _http;
    private readonly IBedrockService _bedrock;
    private readonly string _token;
    private readonly ILogger<ProductHuntParser> _logger;

    public string ParserType => "producthunt";

    public ProductHuntParser(HttpClient http, IBedrockService bedrock, IConfiguration config, ILogger<ProductHuntParser> logger)
    {
        _http = http;
        _bedrock = bedrock;
        _token = config["ProductHunt:Token"] ?? "";
        _logger = logger;
    }

    public async Task<List<RawSignal>> FetchSignalsAsync(ScoutSource source, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_token))
        {
            _logger.LogWarning("PH: No API token configured, skipping");
            return new List<RawSignal>();
        }

        var signals = new List<RawSignal>();

        // Topics come from source auth_config (comma-separated)
        var topics = !string.IsNullOrEmpty(source.AuthConfig)
            ? source.AuthConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { "artificial-intelligence", "productivity", "developer-tools" };

        var allProducts = new List<PhProduct>();

        foreach (var topic in topics)
        {
            try
            {
                var products = await FetchTopicPostsAsync(topic, 10, ct);
                allProducts.AddRange(products);
                _logger.LogInformation("PH: fetched {Count} products from topic '{Topic}'", products.Count, topic);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PH: failed to fetch topic '{Topic}'", topic);
            }
        }

        allProducts = allProducts.DistinctBy(p => p.Name).ToList();
        _logger.LogInformation("PH: {Count} unique products, sending to Bedrock", allProducts.Count);

        var batches = allProducts.Chunk(10).ToList();
        foreach (var batch in batches)
        {
            try
            {
                var extracted = await ExtractPainPointsAsync(batch, source, ct);
                signals.AddRange(extracted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PH: Bedrock analysis failed for batch");
            }
        }

        _logger.LogInformation("PH: extracted {Count} signals", signals.Count);
        return signals;
    }

    private async Task<List<PhProduct>> FetchTopicPostsAsync(string topic, int count, CancellationToken ct)
    {
        var query = new
        {
            query = $@"{{ posts(first: {count}, order: RANKING, topic: ""{topic}"") {{ edges {{ node {{ name tagline votesCount url topics {{ edges {{ node {{ name }} }} }} }} }} }} }}"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.producthunt.com/v2/api/graphql");
        request.Headers.Add("Authorization", $"Bearer {_token}");
        request.Content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var products = new List<PhProduct>();
        var edges = doc.RootElement
            .GetProperty("data")
            .GetProperty("posts")
            .GetProperty("edges");

        foreach (var edge in edges.EnumerateArray())
        {
            var node = edge.GetProperty("node");
            var topicNames = node.GetProperty("topics").GetProperty("edges")
                .EnumerateArray()
                .Select(t => t.GetProperty("node").GetProperty("name").GetString() ?? "")
                .ToList();

            products.Add(new PhProduct
            {
                Name = node.GetProperty("name").GetString() ?? "",
                Tagline = node.GetProperty("tagline").GetString() ?? "",
                Votes = node.GetProperty("votesCount").GetInt32(),
                Url = node.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                Topics = topicNames,
                QueryTopic = topic
            });
        }

        return products;
    }

    private async Task<List<RawSignal>> ExtractPainPointsAsync(
        PhProduct[] products, ScoutSource source, CancellationToken ct)
    {
        var productList = string.Join("\n", products.Select((p, i) =>
            $"[{i + 1}] {p.Name} — \"{p.Tagline}\"\n    Votes: {p.Votes} | Topics: {string.Join(", ", p.Topics)}"));

        var prompt = $@"You are an app opportunity scout analyzing Product Hunt launches. Your job is NOT to copy these products, but to identify the underlying pain points they are trying to solve.

For each product, ask: ""What user problem does this product reveal? Is there a gap, a better approach, or an underserved audience?""

Products:
{productList}

For EACH product that reveals a viable underlying pain point, respond with a JSON array:
[
  {{
    ""index"": 1,
    ""pain_point"": ""the underlying user problem this product attempts to solve"",
    ""potential_app"": ""a different or better approach to solving this pain"",
    ""target_audience"": ""who suffers from this pain most"",
    ""platform_hint"": ""mobile | web | hybrid"",
    ""relevance_score"": 1-10
  }}
]

Rules:
- Focus on the PAIN, not the product
- Only include genuine recurring pain points (relevance_score >= 6)
- Skip products that are just features, toys, or novelty
- Write pain_point in generalized form, not tied to the specific product
- If a product reveals NO clear underlying pain, skip it
- Return ONLY the JSON array";

        var response = await _bedrock.InvokeAsync(prompt, ct);

        var signals = new List<RawSignal>();
        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < 0) return signals;

            var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var items = JsonSerializer.Deserialize<List<PainPointResult>>(jsonStr,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null) return signals;

            foreach (var item in items.Where(i => i.RelevanceScore >= 6))
            {
                var idx = item.Index - 1;
                if (idx < 0 || idx >= products.Length) continue;
                var product = products[idx];

                signals.Add(new RawSignal
                {
                    SourceId = source.Id,
                    Source = $"producthunt/{product.QueryTopic}",
                    Title = item.PotentialApp ?? product.Name,
                    Description = $"PH: {product.Name} — \"{product.Tagline}\" (Votes: {product.Votes}) [Platform: {item.PlatformHint ?? "hybrid"}]",
                    Url = product.Url,
                    PainPoint = item.PainPoint,
                    FrequencyScore = item.RelevanceScore
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PH: failed to parse Bedrock response");
        }

        return signals;
    }

    private class PhProduct
    {
        public string Name { get; set; } = "";
        public string Tagline { get; set; } = "";
        public int Votes { get; set; }
        public string Url { get; set; } = "";
        public List<string> Topics { get; set; } = new();
        public string QueryTopic { get; set; } = "";
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
