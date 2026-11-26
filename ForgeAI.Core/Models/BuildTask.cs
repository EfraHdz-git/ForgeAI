namespace ForgeAI.Core.Models;

public class BuildTask
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public int? ReviewId { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
