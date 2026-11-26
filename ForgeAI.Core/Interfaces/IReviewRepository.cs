using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface IReviewRepository
{
    Task<int> CreateAsync(Review review);
    Task<Review?> GetByIdAsync(int id);
    Task<Review?> GetByTokenAsync(string token);
    Task<IEnumerable<Review>> GetPendingAsync();
    Task UpdateStatusAsync(int id, string status);
}
