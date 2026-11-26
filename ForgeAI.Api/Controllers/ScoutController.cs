using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using ForgeAI.Services.Agents;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScoutController : ControllerBase
{
    private readonly ScoutAgent _scoutAgent;
    private readonly IScoutSourceRepository _sources;
    private readonly IRawSignalRepository _signals;
    private readonly IClusterRepository _clusters;

    public ScoutController(
        ScoutAgent scoutAgent,
        IScoutSourceRepository sources,
        IRawSignalRepository signals,
        IClusterRepository clusters)
    {
        _scoutAgent = scoutAgent;
        _sources = sources;
        _signals = signals;
        _clusters = clusters;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        var result = await _scoutAgent.RunAsync();
        return Ok(result);
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources()
    {
        var sources = await _sources.GetEnabledSourcesAsync();
        return Ok(sources);
    }

    [HttpPost("sources")]
    public async Task<IActionResult> AddSource([FromBody] ScoutSource source)
    {
        var id = await _sources.CreateAsync(source);
        return Ok(new { Id = id });
    }

    [HttpPost("sources/seed")]
    public async Task<IActionResult> SeedDefaults()
    {
        var existing = await _sources.GetEnabledSourcesAsync();
        if (existing.Any()) return Ok(new { Message = "Sources already exist", Count = existing.Count() });

        var defaults = new List<ScoutSource>
        {
            new()
            {
                Name = "Hacker News",
                Type = "news_aggregator",
                BaseUrl = "https://hacker-news.firebaseio.com/v0",
                IsEnabled = true,
                PollingIntervalMinutes = 1440,
                ParserType = "hackernews",
                RateLimit = 30,
                Notes = "Top, Ask, and Show HN stories"
            },
            new()
            {
                Name = "Reddit",
                Type = "forum",
                BaseUrl = "https://www.reddit.com",
                IsEnabled = true,
                PollingIntervalMinutes = 1440,
                ParserType = "reddit",
                RateLimit = 60,
                AuthConfig = "SideProject,startups,AppIdeas,Entrepreneur,smallbusiness",
                Notes = "Hot posts from startup and side project subreddits"
            }
        };

        foreach (var source in defaults)
            await _sources.CreateAsync(source);

        return Ok(new { Message = "Default sources seeded", Count = defaults.Count });
    }

    [HttpGet("signals")]
    public async Task<IActionResult> GetSignals()
    {
        var signals = await _signals.GetUnclusteredAsync();
        return Ok(signals);
    }

    [HttpGet("clusters")]
    public async Task<IActionResult> GetClusters()
    {
        var clusters = await _clusters.GetActiveAsync();
        return Ok(clusters);
    }
}
