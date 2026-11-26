using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IEvaluationRepository
{
    Task<int> CreateAsync(Evaluation evaluation);
    Task<Evaluation?> GetByOpportunityIdAsync(int opportunityId);
    Task<IEnumerable<Evaluation>> GetByRecommendationAsync(string recommendation);
}
