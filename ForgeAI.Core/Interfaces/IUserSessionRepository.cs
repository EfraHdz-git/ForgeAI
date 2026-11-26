using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IUserSessionRepository
{
    Task<int> CreateAsync(UserSession session);
    Task<UserSession?> GetByTokenAsync(string token);
    Task DeleteByTokenAsync(string token);
    Task DeleteAllForUserAsync(int userId);
    Task CleanupExpiredAsync();
}
