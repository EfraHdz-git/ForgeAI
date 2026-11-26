using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IMagicLinkRepository
{
    Task<int> CreateAsync(MagicLink link);
    Task<MagicLink?> GetByTokenAsync(string token);
    Task MarkUsedAsync(int id);
    Task CleanupExpiredAsync();
}
