using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class ClusterRepository : IClusterRepository
{
    private readonly DbConnectionFactory _db;

    public ClusterRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(OpportunityCluster cluster)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO opportunity_clusters (name, summary, signal_count)
            OUTPUT INSERTED.id
            VALUES (@Name, @Summary, @SignalCount)", cluster);
    }

    public async Task<OpportunityCluster?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<OpportunityCluster>(
            "SELECT * FROM opportunity_clusters WHERE id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<OpportunityCluster>> GetActiveAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<OpportunityCluster>(
            "SELECT * FROM opportunity_clusters WHERE status = 'active' ORDER BY last_seen DESC");
    }

    public async Task IncrementSignalCountAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            UPDATE opportunity_clusters
            SET signal_count = signal_count + 1, last_seen = GETUTCDATE()
            WHERE id = @Id", new { Id = id });
    }

    public async Task UpdateSummaryAsync(int id, string summary)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE opportunity_clusters SET summary = @Summary WHERE id = @Id",
            new { Id = id, Summary = summary });
    }
}
