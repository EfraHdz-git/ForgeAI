namespace ForgeAI.Core.Models;

public class Evaluation
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public int? MarketFitScore { get; set; }
    public int? ComplexityScore { get; set; }
    public int? CompetitionScore { get; set; }
    public int? MonetizationScore { get; set; }
    public int? MobileFitScore { get; set; }
    public int? OverallScore { get; set; }
    public string? Summary { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
    public int? ClarityScore { get; set; }
    public DateTime EvaluatedAt { get; set; }
}
