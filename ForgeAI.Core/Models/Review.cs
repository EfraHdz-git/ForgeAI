namespace ForgeAI.Core.Models;

public class Review
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public int? PrototypeId { get; set; }
    public string Status { get; set; } = "pending_review";
    public string? ReviewerToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
