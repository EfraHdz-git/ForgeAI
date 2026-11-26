namespace ForgeAI.Core.DTOs;

public class ReviewDecisionDto
{
    public string Decision { get; set; } = string.Empty; // approve, reject, revision_requested
    public string? ReasonCategory { get; set; }
    public string? Notes { get; set; }
}
