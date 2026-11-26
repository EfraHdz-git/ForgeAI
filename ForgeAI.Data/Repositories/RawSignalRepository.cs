using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class RawSignalRepository : IRawSignalRepository
{
    private readonly DbConnectionFactory _db;

    public RawSignalRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(RawSignal signal)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO raw_signals (source_id, source, title, description, url, pain_point, frequency_score)
            OUTPUT INSERTED.id
            VALUES (@SourceId, @Source, @Title, @Description, @Url, @PainPoint, @FrequencyScore)", signal);
    }

    public async Task<IEnumerable<RawSignal>> GetUnclusteredAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<RawSignal>(
            "SELECT * FROM raw_signals WHERE is_clustered = 0 ORDER BY discovered_at DESC");
    }

    public async Task<IEnumerable<RawSignal>> GetByClusterIdAsync(int clusterId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<RawSignal>(
            "SELECT * FROM raw_signals WHERE cluster_id = @ClusterId", new { ClusterId = clusterId });
    }

    public async Task MarkClusteredAsync(int signalId, int clusterId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE raw_signals SET is_clustered = 1, cluster_id = @ClusterId WHERE id = @Id",
            new { Id = signalId, ClusterId = clusterId });
    }

    public async Task<int> CountSimilarAsync(string painPoint)
    {
        using var conn = _db.CreateConnection();
        // Count signals with similar pain points (fuzzy match via SOUNDEX + LIKE)
        var keywords = painPoint.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4).Take(3).ToList();
        if (keywords.Count == 0) return 0;

        var likeClause = string.Join(" OR ", keywords.Select((k, i) => $"pain_point LIKE @K{i}"));
        var parameters = new Dapper.DynamicParameters();
        for (int i = 0; i < keywords.Count; i++)
            parameters.Add($"K{i}", $"%{keywords[i]}%");

        return await conn.QuerySingleAsync<int>(
            $"SELECT COUNT(DISTINCT id) FROM raw_signals WHERE {likeClause}", parameters);
    }
}
