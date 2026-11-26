using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class PipelineLogRepository : IPipelineLogRepository
{
    private readonly DbConnectionFactory _db;

    public PipelineLogRepository(DbConnectionFactory db) => _db = db;

    public async Task LogAsync(string stage, int? opportunityId, string action, string? details = null)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO pipeline_logs (stage, opportunity_id, action, details)
            VALUES (@Stage, @OpportunityId, @Action, @Details)",
            new { Stage = stage, OpportunityId = opportunityId, Action = action, Details = details });
    }

    public async Task<IEnumerable<PipelineLog>> GetByOpportunityIdAsync(int opportunityId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<PipelineLog>(
            "SELECT * FROM pipeline_logs WHERE opportunity_id = @OpportunityId ORDER BY timestamp DESC",
            new { OpportunityId = opportunityId });
    }

    public async Task<IEnumerable<PipelineLog>> GetRecentAsync(int count = 100)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<PipelineLog>(
            "SELECT TOP (@Count) * FROM pipeline_logs ORDER BY timestamp DESC",
            new { Count = count });
    }
}
