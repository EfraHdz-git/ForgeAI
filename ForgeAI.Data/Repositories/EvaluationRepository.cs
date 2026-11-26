using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class EvaluationRepository : IEvaluationRepository
{
    private readonly DbConnectionFactory _db;

    public EvaluationRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(Evaluation evaluation)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO evaluations (opportunity_id, market_fit_score, complexity_score, competition_score,
                monetization_score, mobile_fit_score, overall_score, summary, recommendation, reasoning)
            OUTPUT INSERTED.id
            VALUES (@OpportunityId, @MarketFitScore, @ComplexityScore, @CompetitionScore,
                @MonetizationScore, @MobileFitScore, @OverallScore, @Summary, @Recommendation, @Reasoning)", evaluation);
    }

    public async Task<Evaluation?> GetByOpportunityIdAsync(int opportunityId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Evaluation>(
            "SELECT * FROM evaluations WHERE opportunity_id = @OpportunityId ORDER BY evaluated_at DESC",
            new { OpportunityId = opportunityId });
    }

    public async Task<IEnumerable<Evaluation>> GetByRecommendationAsync(string recommendation)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Evaluation>(
            "SELECT * FROM evaluations WHERE recommendation = @Recommendation ORDER BY overall_score DESC",
            new { Recommendation = recommendation });
    }
}
