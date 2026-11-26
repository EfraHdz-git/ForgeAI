namespace ForgeAI.Core.Models;

public class DecisionFeedback
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public int? PrototypeId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? ReasonCategory { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
