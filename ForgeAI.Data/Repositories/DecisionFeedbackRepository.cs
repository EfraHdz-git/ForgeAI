using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class DecisionFeedbackRepository : IDecisionFeedbackRepository
{
    private readonly DbConnectionFactory _db;

    public DecisionFeedbackRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(DecisionFeedback feedback)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO decision_feedback (opportunity_id, prototype_id, decision, reason_category, notes)
            OUTPUT INSERTED.id
            VALUES (@OpportunityId, @PrototypeId, @Decision, @ReasonCategory, @Notes)", feedback);
    }

    public async Task<IEnumerable<DecisionFeedback>> GetByOpportunityIdAsync(int opportunityId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DecisionFeedback>(
            "SELECT * FROM decision_feedback WHERE opportunity_id = @OpportunityId ORDER BY created_at DESC",
            new { OpportunityId = opportunityId });
    }

    public async Task<IEnumerable<DecisionFeedback>> GetByCategoryAsync(string reasonCategory)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DecisionFeedback>(
            "SELECT * FROM decision_feedback WHERE reason_category = @ReasonCategory ORDER BY created_at DESC",
            new { ReasonCategory = reasonCategory });
    }
}
