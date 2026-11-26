using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IOpportunityRepository
{
    Task<int> CreateAsync(Opportunity opportunity);
    Task<Opportunity?> GetByIdAsync(int id);
    Task<IEnumerable<Opportunity>> GetByStatusAsync(string status);
    Task UpdateStatusAsync(int id, string status);
    Task<bool> ExistsSimilarAsync(string title);
}
