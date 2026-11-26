using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly DbConnectionFactory _db;

    public ReviewRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(Review review)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO reviews (opportunity_id, prototype_id, status, reviewer_token, expires_at)
            OUTPUT INSERTED.id
            VALUES (@OpportunityId, @PrototypeId, @Status, @ReviewerToken, @ExpiresAt)", review);
    }

    public async Task<Review?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Review>(
            "SELECT * FROM reviews WHERE id = @Id", new { Id = id });
    }

    public async Task<Review?> GetByTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Review>(
            "SELECT * FROM reviews WHERE reviewer_token = @Token AND (expires_at IS NULL OR expires_at > GETUTCDATE())",
            new { Token = token });
    }

    public async Task<IEnumerable<Review>> GetPendingAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Review>(
            "SELECT * FROM reviews WHERE status = 'pending_review' ORDER BY created_at DESC");
    }

    public async Task UpdateStatusAsync(int id, string status)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE reviews SET status = @Status, updated_at = GETUTCDATE() WHERE id = @Id",
            new { Id = id, Status = status });
    }
}
