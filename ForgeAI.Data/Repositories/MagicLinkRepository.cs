using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class MagicLinkRepository : IMagicLinkRepository
{
    private readonly DbConnectionFactory _db;

    public MagicLinkRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(MagicLink link)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO magic_links (email, token, expires_at)
            OUTPUT INSERTED.id
            VALUES (@Email, @Token, @ExpiresAt)", new
        {
            Email = link.Email.ToLowerInvariant(),
            link.Token,
            link.ExpiresAt
        });
    }

    public async Task<MagicLink?> GetByTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<MagicLink>(
            "SELECT * FROM magic_links WHERE token = @Token", new { Token = token });
    }

    public async Task MarkUsedAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE magic_links SET used_at = GETUTCDATE() WHERE id = @Id", new { Id = id });
    }

    public async Task CleanupExpiredAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM magic_links WHERE expires_at < GETUTCDATE() AND used_at IS NULL");
    }
}
