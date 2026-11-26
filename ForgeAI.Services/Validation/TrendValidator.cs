using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Validation;

public class TrendValidator
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<TrendValidator> _logger;

    public TrendValidator(HttpClient http, IConfiguration config, ILogger<TrendValidator> logger)
    {
        _http = http;
        _apiKey = config["SerpApi:ApiKey"] ?? "";
        _logger = logger;
    }

    // ─── Google Trends ───

    public async Task<TrendData?> GetTrendDataAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;
        try
        {
            var url = $"https://serpapi.com/search?engine=google_trends&q={Uri.EscapeDataString(query)}&date=today+12-m&api_key={_apiKey}";
            var doc = await FetchJsonAsync(url, ct);
            var result = new TrendData { Query = query };

            if (doc.RootElement.TryGetProperty("interest_over_time", out var iot)
                && iot.TryGetProperty("timeline_data", out var timeline))
            {
                var values = new List<int>();
                foreach (var point in timeline.EnumerateArray())
                    if (point.TryGetProperty("values", out var vals))
                        foreach (var v in vals.EnumerateArray())
                            if (v.TryGetProperty("extracted_value", out var ev))
                                values.Add(ev.GetInt32());

                if (values.Count > 0)
                {
                    result.AverageInterest = (int)values.Average();
                    result.RecentInterest = (int)values.TakeLast(4).Average();
                    result.PeakInterest = values.Max();
                    var firstQ = values.Take(values.Count / 4).DefaultIfEmpty(0).Average();
                    var lastQ = values.TakeLast(values.Count / 4).DefaultIfEmpty(0).Average();
                    result.TrendDirection = lastQ > firstQ * 1.1 ? "rising" : lastQ < firstQ * 0.9 ? "declining" : "stable";
                }
            }

            if (doc.RootElement.TryGetProperty("related_queries", out var rq) && rq.TryGetProperty("rising", out var rising))
                result.RelatedQueries = rising.EnumerateArray().Take(5)
                    .Select(i => i.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "")
                    .Where(s => s.Length > 0).ToList();

            _logger.LogInformation("Trends '{Query}': avg={Avg}, recent={Recent}, dir={Dir}", query, result.AverageInterest, result.RecentInterest, result.TrendDirection);
            return result;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Trends failed for '{Query}'", query); return null; }
    }

    // ─── Apple App Store ───

    public async Task<StoreSearchResult?> SearchAppleStoreAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;
        try
        {
            var url = $"https://serpapi.com/search?engine=apple_app_store&term={Uri.EscapeDataString(query)}&api_key={_apiKey}";
            var doc = await FetchJsonAsync(url, ct);
            var result = new StoreSearchResult { Query = query, Store = "apple" };

            if (doc.RootElement.TryGetProperty("organic_results", out var results))
            {
                foreach (var app in results.EnumerateArray().Take(5))
                {
                    double rating = 0;
                    int reviewCount = 0;
                    if (app.TryGetProperty("rating", out var ratingArr) && ratingArr.ValueKind == JsonValueKind.Array)
                    {
                        var first = ratingArr.EnumerateArray().FirstOrDefault();
                        if (first.TryGetProperty("rating", out var r)) rating = r.GetDouble();
                        if (first.TryGetProperty("count", out var c)) reviewCount = c.GetInt32();
                    }

                    result.Apps.Add(new StoreApp
                    {
                        Id = app.TryGetProperty("id", out var id) ? id.GetInt64().ToString() : "",
                        Title = app.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Rating = rating,
                        ReviewCount = reviewCount,
                        Downloads = "",
                        Price = app.TryGetProperty("price", out var p) && p.TryGetProperty("type", out var pt) ? pt.GetString() ?? "" : "",
                        Store = "apple"
                    });
                }
            }

            _logger.LogInformation("Apple Store '{Query}': {Count} apps", query, result.Apps.Count);
            return result;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Apple Store search failed for '{Query}'", query); return null; }
    }

    public async Task<List<string>?> GetAppleReviewsAsync(string productId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;
        try
        {
            var url = $"https://serpapi.com/search?engine=apple_reviews&product_id={productId}&api_key={_apiKey}";
            var doc = await FetchJsonAsync(url, ct);
            var reviews = new List<string>();

            if (doc.RootElement.TryGetProperty("reviews", out var reviewsArr))
                foreach (var review in reviewsArr.EnumerateArray().Take(15))
                {
                    var rating = review.TryGetProperty("rating", out var r) ? r.GetInt32() : 5;
                    var text = review.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    var title = review.TryGetProperty("title", out var ti) ? ti.GetString() ?? "" : "";
                    if (rating <= 3 && (text.Length > 20 || title.Length > 10))
                        reviews.Add($"[{rating}★] {title}: {text}".Truncate(300));
                }

            _logger.LogInformation("Apple reviews for '{Id}': {Count} low-rating", productId, reviews.Count);
            return reviews;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Apple reviews failed for '{Id}'", productId); return null; }
    }

    // ─── Google Web Search ───

    public async Task<WebSearchResult?> SearchWebAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;
        try
        {
            var url = $"https://serpapi.com/search?engine=google&q={Uri.EscapeDataString(query)}&num=10&api_key={_apiKey}";
            var doc = await FetchJsonAsync(url, ct);
            var result = new WebSearchResult { Query = query };

            if (doc.RootElement.TryGetProperty("organic_results", out var results))
            {
                foreach (var item in results.EnumerateArray().Take(10))
                {
                    result.Sites.Add(new WebResult
                    {
                        Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Link = item.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "",
                        Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : ""
                    });
                }
            }

            _logger.LogInformation("Web search '{Query}': {Count} results", query, result.Sites.Count);
            return result;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Web search failed for '{Query}'", query); return null; }
    }

    // ─── Google Play Store ───

    public async Task<StoreSearchResult?> SearchPlayStoreAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;
        try
        {
            var url = $"https://serpapi.com/search?engine=google_play&q={Uri.EscapeDataString(query)}&store=apps&api_key={_apiKey}";
            var doc = await FetchJsonAsync(url, ct);
            var result = new StoreSearchResult { Query = query, Store = "google_play" };

            if (doc.RootElement.TryGetProperty("organic_results", out var groups))
                foreach (var group in groups.EnumerateArray())
                    if (group.TryGetProperty("items", out var items))
                        foreach (var app in items.EnumerateArray().Take(5))
                            result.Apps.Add(new StoreApp
                            {
                                Id = app.TryGetProperty("product_id", out var pid) ? pid.GetString() ?? "" : "",
                                Title = app.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                                Rating = app.TryGetProperty("rating", out var r) ? r.GetDouble() : 0,
                                Downloads = app.TryGetProperty("downloads", out var d) ? d.GetString() ?? "" : "",
                                Store = "google_play"
                            });

            _logger.LogInformation("Play Store '{Query}': {Count} apps", query, result.Apps.Count);
            return result;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Play Store search failed for '{Query}'", query); return null; }
    }

    public async Task<List<string>?> GetPlayStoreReviewsAsync(string productId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;
        try
        {
            var url = $"https://serpapi.com/search?engine=google_play_product&product_id={Uri.EscapeDataString(productId)}&store=apps&api_key={_apiKey}";
            var doc = await FetchJsonAsync(url, ct);
            var reviews = new List<string>();

            if (doc.RootElement.TryGetProperty("reviews", out var reviewsArr))
                foreach (var review in reviewsArr.EnumerateArray().Take(15))
                {
                    var rating = review.TryGetProperty("rating", out var r) ? r.GetInt32() : 5;
                    var snippet = review.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                    if (rating <= 3 && snippet.Length > 20)
                        reviews.Add($"[{rating}★] {snippet}".Truncate(300));
                }

            _logger.LogInformation("Play Store reviews for '{Id}': {Count} low-rating", productId, reviews.Count);
            return reviews;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Play Store reviews failed for '{Id}'", productId); return null; }
    }

    // ─── Helper ───

    private async Task<JsonDocument> FetchJsonAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetStringAsync(url, ct);
        return JsonDocument.Parse(response);
    }
}

public class TrendData
{
    public string Query { get; set; } = "";
    public int AverageInterest { get; set; }
    public int RecentInterest { get; set; }
    public int PeakInterest { get; set; }
    public string TrendDirection { get; set; } = "unknown";
    public List<string> RelatedQueries { get; set; } = new();
}

public class WebSearchResult
{
    public string Query { get; set; } = "";
    public List<WebResult> Sites { get; set; } = new();
}

public class WebResult
{
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Snippet { get; set; } = "";
}

public class StoreSearchResult
{
    public string Query { get; set; } = "";
    public string Store { get; set; } = "";
    public List<StoreApp> Apps { get; set; } = new();
}

public class StoreApp
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public string Downloads { get; set; } = "";
    public string Price { get; set; } = "";
    public string Store { get; set; } = "";
}

static class StringTruncateExtension
{
    public static string Truncate(this string s, int max) => s.Length <= max ? s : s[..max] + "...";
}
