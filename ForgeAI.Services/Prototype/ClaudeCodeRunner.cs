using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Prototype;

public class ClaudeCodeRunner
{
    private readonly ILogger<ClaudeCodeRunner> _logger;
    private readonly int _timeoutMinutes;
    private readonly string _prototypesBasePath;

    public ClaudeCodeRunner(IConfiguration config, ILogger<ClaudeCodeRunner> logger)
    {
        _logger = logger;
        _timeoutMinutes = config.GetValue<int>("Pipeline:PrototypeTimeoutMinutes", 10);
        _prototypesBasePath = config["Pipeline:PrototypesBasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prototypes");
    }

    public async Task<ClaudeCodeResult> RunAsync(int opportunityId, string prd, int version = 1, CancellationToken ct = default)
    {
        var dirName = version == 1 ? $"prototype-{opportunityId}" : $"prototype-{opportunityId}-v{version}";
        var projectDir = Path.GetFullPath(Path.Combine(_prototypesBasePath, dirName));
        Directory.CreateDirectory(projectDir);

        // Write PRD to the project directory so Claude Code can reference it
        var prdPath = Path.Combine(projectDir, "PRD.md");
        await File.WriteAllTextAsync(prdPath, prd, ct);

        // Extract app title from PRD for context
        var appTitle = prd.Split('\n').FirstOrDefault(l => l.StartsWith("# "))?.TrimStart('#').Trim() ?? "Prototype";

        var buildPrompt = $@"Read the PRD.md file in this directory and build the prototype it describes.

You are building a focused product prototype for ""{appTitle}"", not a production app.

GOAL:
- Clearly demonstrate the core value proposition
- Make the main user flow obvious and convincing
- Produce a polished, believable prototype that can be reviewed in under 60 seconds

IMPORTANT — START WITH A PITCH PAGE:
The FIRST thing the user sees when opening the app must be a simple pitch page that explains the idea. This is NOT a marketing landing page. It's a plain, clear explanation:

1. THE PROBLEM — one short sentence. What's broken? Who feels it? (e.g., ""Gym beginners forget their workout plan mid-session"")
2. THE SOLUTION — one short sentence. What does this app do? (e.g., ""Shows your next exercise instantly, no scrolling"")
3. HOW IT WORKS — 3 simple steps with icons or short text (e.g., ""1. Pick a plan  2. Start workout  3. Follow step by step"")
4. WHO IT'S FOR — one line (e.g., ""For people who just started going to the gym"")
5. WHY THIS WORKS — 2-3 short bullet points answering ""why should I invest time and money in this?"" (e.g., ""Existing apps are too complicated — top competitor has 2-star reviews about confusing UI"", ""40M gym beginners in the US, growing 12% yearly"", ""Simple subscription model — $5/month"")
6. WHAT'S DIFFERENT — one line about what makes this better than what exists (e.g., ""Other apps try to do everything. This does one thing perfectly."")
7. A big ""Try It"" button that enters the actual app prototype

Rules for the pitch page:
- Use simple words a teenager would understand
- No buzzwords, no jargon, no ""revolutionize"" or ""leverage"" or ""empower""
- Short sentences. No paragraphs.
- Make it feel like you're explaining to a friend, not pitching to investors
- Clean visual design, plenty of whitespace

AFTER the pitch page, build the actual app screens as described in the PRD.

STRICT REQUIREMENTS:
- Use React with Vite (run: npm create vite@latest . -- --template react)
- Build for the platform direction described in PRD.md (mobile, web, or hybrid)
- Mock data only — no backend, no real API calls
- Local state only (useState, useContext only if needed)
- No authentication
- 3-5 app screens max as described in the PRD (plus the pitch page)
- Keep the implementation simple and focused
- Do NOT overengineer or add features outside the PRD
- Use clean, modern UI with strong typography, spacing, and visual hierarchy
- Use realistic sample content — no lorem ipsum or placeholder junk
- Include a package.json with all dependencies
- The app must build successfully with: npm install && npm run build

OUTPUT EXPECTATIONS:
- Pitch page loads first with clear problem/solution/how-it-works
- ""Try It"" button enters the prototype
- Cohesive visual design across pitch and app screens
- Responsive layout appropriate to the platform direction in the PRD
- Believable mock data that supports the concept
- Code organized clearly but minimally

BEFORE FINISHING:
- Ensure the app builds successfully with npm run build
- Ensure the pitch page and main user flow are both implemented
- Write a short BUILD_SUMMARY.md with:
  - Screens implemented
  - Key features built
  - Run command: npm run dev
  - Build command: npm run build

Do NOT ask questions. Read PRD.md, then scaffold and build the complete prototype.";

        var command = $"claude -p \"{buildPrompt.Replace("\"", "\\\"")}\" --no-input";

        _logger.LogInformation("Starting Claude Code for opportunity {Id} in {Dir}", opportunityId, projectDir);
        _logger.LogInformation("Timeout: {Timeout} minutes", _timeoutMinutes);

        var result = new ClaudeCodeResult
        {
            WorkingDirectory = projectDir,
            CommandUsed = "claude -p --dangerously-skip-permissions (prompt via stdin)",
            StartedAt = DateTime.UtcNow
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            // Write prompt to a file to avoid shell escaping issues
            var promptPath = Path.Combine(projectDir, ".forge-prompt.txt");
            await File.WriteAllTextAsync(promptPath, buildPrompt, ct);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "-p --dangerously-skip-permissions",
                WorkingDirectory = projectDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdout.AppendLine(e.Data);
                    if (stdout.Length % 5000 < 200)
                        _logger.LogDebug("Claude Code stdout: {Line}", e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Write prompt to stdin and close it
            await process.StandardInput.WriteAsync(buildPrompt);
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(_timeoutMinutes));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
                result.ExitCode = process.ExitCode;
                result.Success = process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Claude Code timed out after {Timeout} minutes, killing process", _timeoutMinutes);
                try { process.Kill(entireProcessTree: true); } catch { }
                result.ExitCode = -1;
                result.Success = false;
                result.TimedOut = true;
            }

            result.FinishedAt = DateTime.UtcNow;
            result.Stdout = stdout.ToString();
            result.Stderr = stderr.ToString();

            // Scan for generated files
            if (Directory.Exists(projectDir))
            {
                var files = Directory.GetFiles(projectDir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(projectDir, f))
                    .Where(f => !f.StartsWith("node_modules") && !f.StartsWith(".git"))
                    .ToList();
                result.GeneratedFiles = files;

                // Check build readiness
                result.HasPackageJson = files.Any(f => f == "package.json");
                result.HasIndexHtml = files.Any(f => f.Contains("index.html"));
                result.EntryFile = files.FirstOrDefault(f => f.EndsWith("index.html"))
                    ?? files.FirstOrDefault(f => f == "src/App.jsx" || f == "src/App.tsx");

                // A valid dist/ with index.html + assets means npm run build completed.
                // Accept this even if the process timed out afterward (e.g. Claude kept running on trailing tasks).
                var distIndex = Path.Combine(projectDir, "dist", "index.html");
                var distAssets = Path.Combine(projectDir, "dist", "assets");
                var distBuildValid = File.Exists(distIndex) && Directory.Exists(distAssets)
                    && Directory.GetFiles(distAssets).Length > 0;

                result.BuildReady = result.HasPackageJson && (result.Success || distBuildValid);

                if (distBuildValid && !result.Success)
                {
                    _logger.LogInformation(
                        "Opportunity {Id}: dist/ is valid despite non-success exit — marking build as ready.", opportunityId);
                }
            }

            _logger.LogInformation(
                "Claude Code finished for opportunity {Id}: exit={Exit}, files={FileCount}, buildReady={Ready}, duration={Duration}s",
                opportunityId, result.ExitCode, result.GeneratedFiles?.Count ?? 0,
                result.BuildReady, (result.FinishedAt - result.StartedAt)?.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude Code process failed for opportunity {Id}", opportunityId);
            result.ExitCode = -2;
            result.Success = false;
            result.Stderr = ex.Message;
            result.FinishedAt = DateTime.UtcNow;
        }

        return result;
    }
}

public class ClaudeCodeResult
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public string CommandUsed { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int ExitCode { get; set; }
    public bool Success { get; set; }
    public bool TimedOut { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public List<string>? GeneratedFiles { get; set; }
    public bool HasPackageJson { get; set; }
    public bool HasIndexHtml { get; set; }
    public string? EntryFile { get; set; }
    public bool BuildReady { get; set; }
}
