using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class FocusProfileRepository : IFocusProfileRepository
{
    private readonly DbConnectionFactory _db;

    public FocusProfileRepository(DbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<FocusProfile>> GetActiveAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FocusProfile>(
            "SELECT * FROM focus_profiles WHERE is_active = 1");
    }

    public async Task<FocusProfile?> GetByNameAsync(string name)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<FocusProfile>(
            "SELECT * FROM focus_profiles WHERE name = @Name", new { Name = name });
    }
}
