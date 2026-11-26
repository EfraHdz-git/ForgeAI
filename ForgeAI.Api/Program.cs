using ForgeAI.Core.Interfaces;
using ForgeAI.Data;
using ForgeAI.Data.Repositories;
using ForgeAI.Services;
using ForgeAI.Services.Agents;
using ForgeAI.Services.Orchestrator;
using ForgeAI.Services.Parsers;
using ForgeAI.Services.Notifications;
using ForgeAI.Services.Prototype;
using ForgeAI.Api.Middleware;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("ForgeAI")
    ?? throw new InvalidOperationException("Missing ForgeAI connection string");
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

// Repositories
builder.Services.AddScoped<IScoutSourceRepository, ScoutSourceRepository>();
builder.Services.AddScoped<IRawSignalRepository, RawSignalRepository>();
builder.Services.AddScoped<IClusterRepository, ClusterRepository>();
builder.Services.AddScoped<IOpportunityRepository, OpportunityRepository>();
builder.Services.AddScoped<IEvaluationRepository, EvaluationRepository>();
builder.Services.AddScoped<IPrototypeRepository, PrototypeRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IAgentRunRepository, AgentRunRepository>();
builder.Services.AddScoped<IPipelineLogRepository, PipelineLogRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IDecisionFeedbackRepository, DecisionFeedbackRepository>();
builder.Services.AddScoped<IFocusProfileRepository, FocusProfileRepository>();

// Bedrock
builder.Services.AddSingleton<IBedrockService, BedrockService>();

// Source parsers
builder.Services.AddHttpClient<HackerNewsParser>();
builder.Services.AddScoped<ISourceParser, HackerNewsParser>();
builder.Services.AddHttpClient<ProductHuntParser>();
builder.Services.AddScoped<ISourceParser, ProductHuntParser>();

// Validation
builder.Services.AddHttpClient<ForgeAI.Services.Validation.TrendValidator>();
builder.Services.AddScoped<ForgeAI.Services.Validation.TrendValidator>();

// SSE + Notifications
builder.Services.AddSingleton<ISseBroadcaster, ForgeAI.Api.Controllers.SseBroadcasterService>();
builder.Services.AddSingleton<EmailService>();

// Auth
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMagicLinkRepository, MagicLinkRepository>();
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
builder.Services.AddSingleton<ForgeAI.Services.Auth.JwtService>();
builder.Services.AddScoped<ForgeAI.Services.Auth.MagicLinkService>();

// Sharing
builder.Services.AddScoped<IShareTokenRepository, ShareTokenRepository>();
builder.Services.AddScoped<ForgeAI.Services.Sharing.ShareService>();

// Market Intelligence (Claude Code web research)
builder.Services.AddSingleton<ForgeAI.Services.MarketIntelligence.ClaudeCodeMarketResearcher>();
builder.Services.AddScoped<ForgeAI.Services.MarketIntelligence.MarketIntelligenceAgent>();

// Prototype services
builder.Services.AddScoped<PrdGenerator>();
builder.Services.AddSingleton<ClaudeCodeRunner>();

// Agents
builder.Services.AddScoped<ScoutAgent>();
builder.Services.AddScoped<EvaluatorAgent>();
builder.Services.AddScoped<PrototypeAgent>();

// Orchestrator disabled — pipeline runs manually from dashboard
// builder.Services.AddHostedService<PipelineOrchestrator>();

// API
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();
app.UseMiddleware<JwtAuthMiddleware>();
app.UseMiddleware<PreviewMiddleware>();
app.MapControllers();

// Serve dashboard at root
var dashboardPath = builder.Configuration["Pipeline:DashboardPath"];
if (dashboardPath != null && Directory.Exists(dashboardPath))
{
    var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dashboardPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
    // SPA fallback
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/preview"))
            return;
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(dashboardPath, "index.html"));
    });
}

// Health check
app.MapGet("/api/health", () => new
{
    Status = "healthy",
    Service = "ForgeAI",
    Timestamp = DateTime.UtcNow
});

app.Run();
