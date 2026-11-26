using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IPrototypeRepository
{
    Task<int> CreateAsync(Prototype prototype);
    Task<Prototype?> GetByIdAsync(int id);
    Task<Prototype?> GetByOpportunityIdAsync(int opportunityId);
    Task UpdateStatusAsync(int id, string status, string? errorMessage = null);
    Task UpdateDeploymentAsync(int id, string deployUrl, string previewPath);
    Task UpdateExecutionMetadataAsync(int id, PrototypeExecutionMetadata metadata);
}

public class PrototypeExecutionMetadata
{
    public string? CommandUsed { get; set; }
    public string? WorkingDirectory { get; set; }
    public int ExitCode { get; set; }
    public string? StdoutLog { get; set; }
    public string? StderrLog { get; set; }
    public string? GeneratedFiles { get; set; }
    public string? BuildCommand { get; set; }
    public string? RunCommand { get; set; }
    public string? EntryFile { get; set; }
    public bool BuildReady { get; set; }
    public DateTime BuildFinishedAt { get; set; }
}
