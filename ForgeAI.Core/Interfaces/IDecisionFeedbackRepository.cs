using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IDecisionFeedbackRepository
{
    Task<int> CreateAsync(DecisionFeedback feedback);
    Task<IEnumerable<DecisionFeedback>> GetByOpportunityIdAsync(int opportunityId);
    Task<IEnumerable<DecisionFeedback>> GetByCategoryAsync(string reasonCategory);
}
