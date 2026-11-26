using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DbConnectionFactory _db;

    public UserRepository(DbConnectionFactory db) => _db = db;

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id", new { Id = id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email", new { Email = email.ToLowerInvariant() });
    }

    public async Task<int> CreateAsync(User user)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO users (email, name, password_hash, tier, email_verified, email_verify_token)
            OUTPUT INSERTED.id
            VALUES (@Email, @Name, @PasswordHash, @Tier, @EmailVerified, @EmailVerifyToken)", new
        {
            Email = user.Email.ToLowerInvariant(),
            user.Name,
            user.PasswordHash,
            user.Tier,
            user.EmailVerified,
            user.EmailVerifyToken
        });
    }

    public async Task<User?> GetByVerifyTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email_verify_token = @Token", new { Token = token });
    }

    public async Task MarkEmailVerifiedAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET email_verified = 1, email_verify_token = NULL WHERE id = @Id",
            new { Id = id });
    }

    public async Task UpdatePasswordAsync(int id, string passwordHash)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET password_hash = @Hash WHERE id = @Id",
            new { Id = id, Hash = passwordHash });
    }

    public async Task UpdateLastLoginAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET last_login_at = GETUTCDATE() WHERE id = @Id", new { Id = id });
    }

    public async Task UpdateTierAsync(int id, string tier)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET tier = @Tier WHERE id = @Id", new { Id = id, Tier = tier });
    }

    public async Task IncrementReportsUsedAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET reports_used = reports_used + 1 WHERE id = @Id", new { Id = id });
    }

    public async Task IncrementPrototypesUsedAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET prototypes_used = prototypes_used + 1 WHERE id = @Id", new { Id = id });
    }

    public async Task ResetMonthlyUsageAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE users SET reports_used = 0, prototypes_used = 0, reports_reset_at = GETUTCDATE()
            WHERE id = @Id", new { Id = id });
    }
}
