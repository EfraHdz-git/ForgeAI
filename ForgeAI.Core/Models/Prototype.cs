namespace ForgeAI.Core.Models;

public class Prototype
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public int? EvaluationId { get; set; }
    public string? PrdContent { get; set; }
    public string? DeployUrl { get; set; }
    public string? LocalProjectPath { get; set; }
    public string? BuildOutputPath { get; set; }
    public string? PreviewPath { get; set; }
    public string DeployProvider { get; set; } = "local";
    public string Status { get; set; } = "queued";
    public string? ClaudeCodeLog { get; set; }
    public string? ErrorMessage { get; set; }
    public int VersionNumber { get; set; } = 1;
    public DateTime? BuildStartedAt { get; set; }
    public DateTime? BuildFinishedAt { get; set; }
    public string? CommandUsed { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? ExitCode { get; set; }
    public string? StdoutLog { get; set; }
    public string? StderrLog { get; set; }
    public string? GeneratedFiles { get; set; }
    public string? BuildCommand { get; set; }
    public string? RunCommand { get; set; }
    public string? EntryFile { get; set; }
    public bool BuildReady { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeployedAt { get; set; }
}
