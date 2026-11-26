using System.Security.Cryptography;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Sharing;

public class ShareService
{
    private readonly IShareTokenRepository _tokens;
    private readonly IOpportunityRepository _opportunities;
    private readonly ILogger<ShareService> _logger;
    private readonly string _baseUrl;
    private readonly int _defaultExpirationDays;

    public ShareService(
        IShareTokenRepository tokens,
        IOpportunityRepository opportunities,
        IConfiguration config,
        ILogger<ShareService> logger)
    {
        _tokens = tokens;
        _opportunities = opportunities;
        _logger = logger;
        _baseUrl = config["App:BaseUrl"] ?? "https://forgeai.codetrend.com";
        _defaultExpirationDays = config.GetValue<int>("Sharing:DefaultExpirationDays", 7);
    }

    /// <summary>
    /// Create a share token for a report or prototype. Validates ownership.
    /// </summary>
    public async Task<ShareResult?> CreateShareAsync(int opportunityId, int userId, string type)
    {
        var opp = await _opportunities.GetByIdAsync(opportunityId);
        if (opp == null || opp.UserId != userId) return null;

        var token = GenerateToken();
        var expires = DateTime.UtcNow.AddDays(_defaultExpirationDays);

        var id = await _tokens.CreateAsync(new ShareToken
        {
            OpportunityId = opportunityId,
            UserId = userId,
            Token = token,
            Type = type,
            ExpiresAt = expires
        });

        var path = type == "prototype" ? $"/preview/{opportunityId}/?share={token}" : $"/?share={token}";
        _logger.LogInformation("Share created: type={Type}, opp={Opp}, token={Token}", type, opportunityId, token[..8]);

        return new ShareResult { Id = id, Token = token, Url = $"{_baseUrl}{path}", ExpiresAt = expires };
    }

    /// <summary>
    /// Validates a token. Returns the token record if valid (active + not expired).
    /// </summary>
    public async Task<ShareToken?> ValidateAsync(string token)
    {
        var record = await _tokens.GetByTokenAsync(token);
        if (record == null) return null;
        if (record.ExpiresAt != null && record.ExpiresAt < DateTime.UtcNow) return null;
        await _tokens.IncrementViewAsync(record.Id);
        return record;
    }

    public Task<IEnumerable<ShareToken>> GetSharesForOpportunityAsync(int opportunityId)
        => _tokens.GetByOpportunityAsync(opportunityId);

    public Task RevokeAsync(int shareId) => _tokens.RevokeAsync(shareId);

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public class ShareResult
{
    public int Id { get; set; }
    public string Token { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}
