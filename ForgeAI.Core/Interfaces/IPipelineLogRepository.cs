using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IPipelineLogRepository
{
    Task LogAsync(string stage, int? opportunityId, string action, string? details = null);
    Task<IEnumerable<PipelineLog>> GetByOpportunityIdAsync(int opportunityId);
    Task<IEnumerable<PipelineLog>> GetRecentAsync(int count = 100);
}
