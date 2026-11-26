using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IScoutSourceRepository
{
    Task<IEnumerable<ScoutSource>> GetEnabledSourcesAsync();
    Task<ScoutSource?> GetByIdAsync(int id);
    Task<int> CreateAsync(ScoutSource source);
    Task UpdateLastRunAsync(int id, DateTime lastRunAt);
}
