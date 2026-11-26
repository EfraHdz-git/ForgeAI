using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class ShareTokenRepository : IShareTokenRepository
{
    private readonly DbConnectionFactory _db;

    public ShareTokenRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(ShareToken token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO share_tokens (opportunity_id, user_id, token, type, expires_at)
            OUTPUT INSERTED.id
            VALUES (@OpportunityId, @UserId, @Token, @Type, @ExpiresAt)", token);
    }

    public async Task<ShareToken?> GetByTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ShareToken>(
            "SELECT * FROM share_tokens WHERE token = @Token AND is_active = 1", new { Token = token });
    }

    public async Task<IEnumerable<ShareToken>> GetByOpportunityAsync(int opportunityId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ShareToken>(
            "SELECT * FROM share_tokens WHERE opportunity_id = @Id AND is_active = 1 ORDER BY created_at DESC",
            new { Id = opportunityId });
    }

    public async Task RevokeAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("UPDATE share_tokens SET is_active = 0 WHERE id = @Id", new { Id = id });
    }

    public async Task IncrementViewAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE share_tokens SET view_count = view_count + 1, last_viewed_at = GETUTCDATE() WHERE id = @Id",
            new { Id = id });
    }
}
