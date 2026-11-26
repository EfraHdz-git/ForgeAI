using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IClusterRepository
{
    Task<int> CreateAsync(OpportunityCluster cluster);
    Task<OpportunityCluster?> GetByIdAsync(int id);
    Task<IEnumerable<OpportunityCluster>> GetActiveAsync();
    Task IncrementSignalCountAsync(int id);
    Task UpdateSummaryAsync(int id, string summary);
}
