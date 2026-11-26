namespace ForgeAI.Core.DTOs;

public class ScoutRunResultDto
{
    public int AgentRunId { get; set; }
    public int SignalsDiscovered { get; set; }
    public int ClustersCreated { get; set; }
    public int OpportunitiesCreated { get; set; }
    public string Status { get; set; } = string.Empty;
}
