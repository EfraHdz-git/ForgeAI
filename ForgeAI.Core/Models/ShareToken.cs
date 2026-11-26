namespace ForgeAI.Core.Models;

public class ShareToken
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Type { get; set; } = "report"; // report | prototype
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public int ViewCount { get; set; }
    public DateTime? LastViewedAt { get; set; }
}
