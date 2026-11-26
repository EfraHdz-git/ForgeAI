using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class OpportunityRepository : IOpportunityRepository
{
    private readonly DbConnectionFactory _db;

    public OpportunityRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(Opportunity opportunity)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO opportunities (cluster_id, title, plain_title, problem_statement, target_audience, why_now, why_mobile_first, status, user_type, moment_of_pain, bad_workaround, simple_use_case, focus_profile_id, user_id)
            OUTPUT INSERTED.id
            VALUES (@ClusterId, @Title, @PlainTitle, @ProblemStatement, @TargetAudience, @WhyNow, @WhyMobileFirst, @Status, @UserType, @MomentOfPain, @BadWorkaround, @SimpleUseCase, @FocusProfileId, @UserId)", opportunity);
    }

    public async Task<Opportunity?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Opportunity>(
            "SELECT * FROM opportunities WHERE id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<Opportunity>> GetByStatusAsync(string status)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Opportunity>(
            "SELECT * FROM opportunities WHERE status = @Status ORDER BY created_at DESC",
            new { Status = status });
    }

    public async Task UpdateStatusAsync(int id, string status)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE opportunities SET status = @Status, updated_at = GETUTCDATE() WHERE id = @Id",
            new { Id = id, Status = status });
    }

    public async Task<bool> ExistsSimilarAsync(string title)
    {
        using var conn = _db.CreateConnection();
        var count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM opportunities WHERE LOWER(title) = LOWER(@Title) OR SOUNDEX(title) = SOUNDEX(@Title)",
            new { Title = title });
        return count > 0;
    }
}
