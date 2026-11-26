using System.Security.Cryptography;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using ForgeAI.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Auth;

public class MagicLinkService
{
    private readonly IMagicLinkRepository _links;
    private readonly IUserRepository _users;
    private readonly EmailService _email;
    private readonly ILogger<MagicLinkService> _logger;
    private readonly string _baseUrl;
    private readonly int _expirationMinutes;

    public MagicLinkService(
        IMagicLinkRepository links,
        IUserRepository users,
        EmailService email,
        IConfiguration config,
        ILogger<MagicLinkService> logger)
    {
        _links = links;
        _users = users;
        _email = email;
        _logger = logger;
        _baseUrl = config["App:BaseUrl"] ?? "https://forgeai.example.com";
        _expirationMinutes = config.GetValue<int>("Auth:MagicLinkExpirationMinutes", 15);
    }

    public async Task SendMagicLinkAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var token = GenerateToken();

        var link = new MagicLink
        {
            Email = email,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes)
        };

        await _links.CreateAsync(link);

        var verifyUrl = $"{_baseUrl}/auth/verify?token={token}";
        await _email.SendMagicLinkAsync(email, verifyUrl);

        _logger.LogInformation("Magic link sent to {Email}", email);
    }

    public async Task SendPasswordResetAsync(string email)
    {
        email = email.Trim().ToLowerInvariant();
        var token = GenerateToken();

        var link = new MagicLink
        {
            Email = email,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes)
        };

        await _links.CreateAsync(link);

        var resetUrl = $"{_baseUrl}/?reset={token}";
        await _email.SendPasswordResetAsync(email, resetUrl);

        _logger.LogInformation("Password reset link sent to {Email}", email);
    }

    public async Task<User?> VerifyTokenAsync(string token)
    {
        var link = await _links.GetByTokenAsync(token);

        if (link == null)
        {
            _logger.LogWarning("Magic link not found: {Token}", token[..8]);
            return null;
        }

        if (link.UsedAt != null)
        {
            _logger.LogWarning("Magic link already used: {Token}", token[..8]);
            return null;
        }

        if (link.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Magic link expired: {Token}", token[..8]);
            return null;
        }

        await _links.MarkUsedAsync(link.Id);

        // Find or create user
        var user = await _users.GetByEmailAsync(link.Email);
        if (user == null)
        {
            var userId = await _users.CreateAsync(new User { Email = link.Email, Tier = "free" });
            user = await _users.GetByIdAsync(userId);
            _logger.LogInformation("New user created: {Email} (id={Id})", link.Email, userId);
        }

        await _users.UpdateLastLoginAsync(user!.Id);
        return user;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
