using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<int> CreateAsync(User user);
    Task<User?> GetByVerifyTokenAsync(string token);
    Task MarkEmailVerifiedAsync(int id);
    Task UpdatePasswordAsync(int id, string passwordHash);
    Task UpdateLastLoginAsync(int id);
    Task UpdateTierAsync(int id, string tier);
    Task IncrementReportsUsedAsync(int id);
    Task IncrementPrototypesUsedAsync(int id);
    Task ResetMonthlyUsageAsync(int id);
}
