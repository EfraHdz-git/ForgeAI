using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class ScoutSourceRepository : IScoutSourceRepository
{
    private readonly DbConnectionFactory _db;

    public ScoutSourceRepository(DbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<ScoutSource>> GetEnabledSourcesAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ScoutSource>(
            "SELECT * FROM scout_sources WHERE is_enabled = 1 ORDER BY name");
    }

    public async Task<ScoutSource?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ScoutSource>(
            "SELECT * FROM scout_sources WHERE id = @Id", new { Id = id });
    }

    public async Task<int> CreateAsync(ScoutSource source)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO scout_sources (name, type, base_url, is_enabled, polling_interval_minutes, parser_type, auth_config, rate_limit, notes)
            OUTPUT INSERTED.id
            VALUES (@Name, @Type, @BaseUrl, @IsEnabled, @PollingIntervalMinutes, @ParserType, @AuthConfig, @RateLimit, @Notes)", source);
    }

    public async Task UpdateLastRunAsync(int id, DateTime lastRunAt)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE scout_sources SET last_run_at = @LastRunAt WHERE id = @Id",
            new { Id = id, LastRunAt = lastRunAt });
    }
}
