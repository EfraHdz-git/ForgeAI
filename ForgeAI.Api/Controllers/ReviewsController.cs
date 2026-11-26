using ForgeAI.Core.DTOs;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ForgeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewRepository _reviews;
    private readonly IOpportunityRepository _opportunities;
    private readonly IEvaluationRepository _evaluations;
    private readonly IPrototypeRepository _prototypes;
    private readonly IDecisionFeedbackRepository _feedback;
    private readonly IPipelineLogRepository _pipelineLog;

    public ReviewsController(
        IReviewRepository reviews,
        IOpportunityRepository opportunities,
        IEvaluationRepository evaluations,
        IPrototypeRepository prototypes,
        IDecisionFeedbackRepository feedback,
        IPipelineLogRepository pipelineLog)
    {
        _reviews = reviews;
        _opportunities = opportunities;
        _evaluations = evaluations;
        _prototypes = prototypes;
        _feedback = feedback;
        _pipelineLog = pipelineLog;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var reviews = await _reviews.GetPendingAsync();
        var cards = new List<ReviewCardDto>();

        foreach (var review in reviews)
        {
            var opp = await _opportunities.GetByIdAsync(review.OpportunityId);
            var eval = await _evaluations.GetByOpportunityIdAsync(review.OpportunityId);
            var proto = review.PrototypeId.HasValue
                ? await _prototypes.GetByIdAsync(review.PrototypeId.Value)
                : null;

            if (opp == null) continue;

            cards.Add(new ReviewCardDto
            {
                ReviewId = review.Id,
                OpportunityId = opp.Id,
                Title = opp.Title,
                ProblemStatement = opp.ProblemStatement,
                TargetAudience = opp.TargetAudience,
                WhyNow = opp.WhyNow,
                WhyMobileFirst = opp.WhyMobileFirst,
                OverallScore = eval?.OverallScore,
                EvaluationSummary = eval?.Summary,
                PrototypeUrl = proto?.DeployUrl,
                ReviewerToken = review.ReviewerToken,
                CreatedAt = review.CreatedAt
            });
        }

        return Ok(cards);
    }

    [HttpPost("{reviewId}/decide")]
    public async Task<IActionResult> Decide(int reviewId, [FromBody] ReviewDecisionDto decision)
    {
        var review = await _reviews.GetByIdAsync(reviewId);
        if (review == null) return NotFound();
        if (review.Status != "pending_review") return BadRequest("Review is not pending");

        var newStatus = decision.Decision switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            "revise" => "revision_requested",
            _ => throw new ArgumentException($"Invalid decision: {decision.Decision}")
        };

        await _reviews.UpdateStatusAsync(reviewId, newStatus);

        // Update opportunity status
        var oppStatus = decision.Decision switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            "revise" => "revision_requested",
            _ => review.Status
        };
        await _opportunities.UpdateStatusAsync(review.OpportunityId, oppStatus);

        // Log feedback
        await _feedback.CreateAsync(new DecisionFeedback
        {
            OpportunityId = review.OpportunityId,
            PrototypeId = review.PrototypeId,
            Decision = decision.Decision,
            ReasonCategory = decision.ReasonCategory,
            Notes = decision.Notes
        });

        await _pipelineLog.LogAsync("review", review.OpportunityId, $"decision_{decision.Decision}",
            $"Reason: {decision.ReasonCategory}. Notes: {decision.Notes}");

        await SseController.BroadcastAsync("review.completed", new
        {
            ReviewId = reviewId,
            OpportunityId = review.OpportunityId,
            Decision = decision.Decision,
            Status = newStatus
        });

        return Ok(new { ReviewId = reviewId, Status = newStatus });
    }
}
