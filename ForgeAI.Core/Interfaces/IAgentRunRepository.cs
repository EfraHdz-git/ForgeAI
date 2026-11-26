using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IAgentRunRepository
{
    Task<int> CreateAsync(AgentRun run);
    Task<AgentRun?> GetByIdAsync(int id);
    Task UpdateStatusAsync(int id, string status, string? error = null);
    Task<IEnumerable<AgentRun>> GetByJobTypeAsync(string jobType);
    Task<IEnumerable<AgentRun>> GetRecentAsync(int count = 50);
}
