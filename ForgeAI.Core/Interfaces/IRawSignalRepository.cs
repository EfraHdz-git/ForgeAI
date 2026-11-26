using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IRawSignalRepository
{
    Task<int> CreateAsync(RawSignal signal);
    Task<IEnumerable<RawSignal>> GetUnclusteredAsync();
    Task<IEnumerable<RawSignal>> GetByClusterIdAsync(int clusterId);
    Task MarkClusteredAsync(int signalId, int clusterId);
    Task<int> CountSimilarAsync(string painPoint);
}
