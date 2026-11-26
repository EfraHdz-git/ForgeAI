namespace ForgeAI.Core.DTOs;

public class PipelineStatusDto
{
    public int PendingEvaluation { get; set; }
    public int Evaluated { get; set; }
    public int PrototypeQueued { get; set; }
    public int Deployed { get; set; }
    public int PendingReview { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int TotalOpportunities { get; set; }
}
