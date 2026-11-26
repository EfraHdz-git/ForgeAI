namespace ForgeAI.Core.Models;

public class PipelineLog
{
    public int Id { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int? OpportunityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
