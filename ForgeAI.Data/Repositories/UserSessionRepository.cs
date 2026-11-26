using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class UserSessionRepository : IUserSessionRepository
{
    private readonly DbConnectionFactory _db;

    public UserSessionRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(UserSession session)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO user_sessions (user_id, token, expires_at)
            OUTPUT INSERTED.id
            VALUES (@UserId, @Token, @ExpiresAt)", session);
    }

    public async Task<UserSession?> GetByTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<UserSession>(
            "SELECT * FROM user_sessions WHERE token = @Token AND expires_at > GETUTCDATE()",
            new { Token = token });
    }

    public async Task DeleteByTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM user_sessions WHERE token = @Token", new { Token = token });
    }

    public async Task DeleteAllForUserAsync(int userId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM user_sessions WHERE user_id = @UserId", new { UserId = userId });
    }

    public async Task CleanupExpiredAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM user_sessions WHERE expires_at < GETUTCDATE()");
    }
}
