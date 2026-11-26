namespace ForgeAI.Core.Models;

public class Opportunity
{
    public int Id { get; set; }
    public int? ClusterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ProblemStatement { get; set; }
    public string? TargetAudience { get; set; }
    public string? WhyNow { get; set; }
    public string? WhyMobileFirst { get; set; }
    public string Status { get; set; } = "pending_evaluation";
    public string? UserType { get; set; }
    public string? MomentOfPain { get; set; }
    public string? BadWorkaround { get; set; }
    public string? SimpleUseCase { get; set; }
    public string? PlainTitle { get; set; }
    public int? FocusProfileId { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
