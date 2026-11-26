using ForgeAI.Core.Interfaces;
using ForgeAI.Services.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Orchestrator;

public class PipelineOrchestrator : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PipelineOrchestrator> _logger;
    private readonly TimeSpan _interval;

    public PipelineOrchestrator(IServiceProvider services, IConfiguration config, ILogger<PipelineOrchestrator> logger)
    {
        _services = services;
        _logger = logger;
        var minutes = config.GetValue<int>("Pipeline:IntervalMinutes", 1440);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pipeline orchestrator started. Interval: {Interval}. First cycle will run after one interval — not on startup.", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await RunPipelineCycleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline cycle failed");
            }
        }
    }

    private bool IsDeepRunDay()
    {
        // Deep run on Mondays and Thursdays (twice per week)
        var day = DateTime.UtcNow.DayOfWeek;
        return day == DayOfWeek.Monday || day == DayOfWeek.Thursday;
    }

    private async Task RunPipelineCycleAsync()
    {
        var isDeep = IsDeepRunDay();
        _logger.LogInformation("Pipeline cycle started at {Time} — Mode: {Mode}",
            DateTime.UtcNow, isDeep ? "DEEP (scout + evaluate + market intel)" : "LIGHT (scout + initial evaluate)");

        using var scope = _services.CreateScope();
        var pipelineLog = scope.ServiceProvider.GetRequiredService<IPipelineLogRepository>();

        // Step 1: Run scout
        try
        {
            var scout = scope.ServiceProvider.GetRequiredService<ScoutAgent>();
            var scoutResult = await scout.RunAsync();
            _logger.LogInformation("Scout completed: {Signals} signals, {Clusters} clusters, {Opps} opportunities",
                scoutResult.SignalsDiscovered, scoutResult.ClustersCreated, scoutResult.OpportunitiesCreated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scout step failed");
            await pipelineLog.LogAsync("orchestrator", null, "scout_failed", ex.Message);
        }

        // Step 2: Run evaluator
        try
        {
            var evaluator = scope.ServiceProvider.GetRequiredService<EvaluatorAgent>();
            var evaluated = await evaluator.RunAsync();
            _logger.LogInformation("Evaluator completed: {Count} opportunities evaluated", evaluated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluator step failed");
            await pipelineLog.LogAsync("orchestrator", null, "evaluator_failed", ex.Message);
        }

        // Step 3+4: Prototype + Deploy only on deep runs or when items are queued
        if (isDeep)
        {
            // Step 3: Run prototype generator (only builds user-approved ideas)
            try
            {
                var protoAgent = scope.ServiceProvider.GetRequiredService<PrototypeAgent>();
                var generated = await protoAgent.RunAsync();
                _logger.LogInformation("Prototype agent completed: {Count} prototypes built", generated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prototype step failed");
                await pipelineLog.LogAsync("orchestrator", null, "prototype_failed", ex.Message);
            }

        // Step 4: Auto-deploy any built prototypes
        try
        {
            var opportunities = scope.ServiceProvider.GetRequiredService<IOpportunityRepository>();
            var prototypes = scope.ServiceProvider.GetRequiredService<IPrototypeRepository>();
            var built = await opportunities.GetByStatusAsync("prototype_built");

            foreach (var opp in built)
            {
                var proto = await prototypes.GetByOpportunityIdAsync(opp.Id);
                if (proto == null || !proto.BuildReady) continue;

                var previewUrl = $"/preview/{opp.Id}/";

                // Patch index.html for relative paths
                var distPath = proto.BuildOutputPath ?? proto.LocalProjectPath + "/dist";
                var indexPath = System.IO.Path.Combine(distPath, "index.html");
                if (System.IO.File.Exists(indexPath))
                {
                    var html = await System.IO.File.ReadAllTextAsync(indexPath);
                    html = html.Replace("href=\"/assets/", "href=\"./assets/");
                    html = html.Replace("src=\"/assets/", "src=\"./assets/");
                    html = html.Replace("href=\"/vite.svg\"", "href=\"./vite.svg\"");
                    await System.IO.File.WriteAllTextAsync(indexPath, html);
                }

                await prototypes.UpdateDeploymentAsync(proto.Id, previewUrl, previewUrl);
                await opportunities.UpdateStatusAsync(opp.Id, "pending_review");
                await pipelineLog.LogAsync("deploy", opp.Id, "auto_deployed",
                    $"Preview: {previewUrl}");
                _logger.LogInformation("Auto-deployed prototype for '{Title}' at {Url}", opp.Title, previewUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-deploy step failed");
            await pipelineLog.LogAsync("orchestrator", null, "deploy_failed", ex.Message);
        }
        } // end isDeep

        _logger.LogInformation("Pipeline cycle completed at {Time} — Mode: {Mode}", DateTime.UtcNow, isDeep ? "DEEP" : "LIGHT");
    }
}
