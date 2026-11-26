using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IShareTokenRepository
{
    Task<int> CreateAsync(ShareToken token);
    Task<ShareToken?> GetByTokenAsync(string token);
    Task<IEnumerable<ShareToken>> GetByOpportunityAsync(int opportunityId);
    Task RevokeAsync(int id);
    Task IncrementViewAsync(int id);
}
