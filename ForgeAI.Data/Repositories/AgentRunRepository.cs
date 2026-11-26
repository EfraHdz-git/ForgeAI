using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class AgentRunRepository : IAgentRunRepository
{
    private readonly DbConnectionFactory _db;

    public AgentRunRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(AgentRun run)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO agent_runs (job_type, related_entity_type, related_entity_id, status, started_at, payload_json)
            OUTPUT INSERTED.id
            VALUES (@JobType, @RelatedEntityType, @RelatedEntityId, @Status, @StartedAt, @PayloadJson)", run);
    }

    public async Task<AgentRun?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AgentRun>(
            "SELECT * FROM agent_runs WHERE id = @Id", new { Id = id });
    }

    public async Task UpdateStatusAsync(int id, string status, string? error = null)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE agent_runs SET status = @Status, error = @Error,
                completed_at = CASE WHEN @Status IN ('completed','failed','cancelled') THEN GETUTCDATE() ELSE completed_at END
            WHERE id = @Id",
            new { Id = id, Status = status, Error = error });
    }

    public async Task<IEnumerable<AgentRun>> GetByJobTypeAsync(string jobType)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AgentRun>(
            "SELECT * FROM agent_runs WHERE job_type = @JobType ORDER BY created_at DESC",
            new { JobType = jobType });
    }

    public async Task<IEnumerable<AgentRun>> GetRecentAsync(int count = 50)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<AgentRun>(
            "SELECT TOP (@Count) * FROM agent_runs ORDER BY created_at DESC",
            new { Count = count });
    }
}
