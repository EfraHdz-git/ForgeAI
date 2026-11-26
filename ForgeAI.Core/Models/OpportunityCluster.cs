namespace ForgeAI.Core.Models;

public class OpportunityCluster
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public int SignalCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string Status { get; set; } = "active";
}
