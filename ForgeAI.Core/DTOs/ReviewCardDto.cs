namespace ForgeAI.Core.DTOs;

public class ReviewCardDto
{
    public int ReviewId { get; set; }
    public int OpportunityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ProblemStatement { get; set; }
    public string? TargetAudience { get; set; }
    public string? WhyNow { get; set; }
    public string? WhyMobileFirst { get; set; }
    public int? OverallScore { get; set; }
    public string? EvaluationSummary { get; set; }
    public string? PrototypeUrl { get; set; }
    public string? ReviewerToken { get; set; }
    public DateTime CreatedAt { get; set; }
}
