namespace ForgeAI.Core.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public string? PasswordHash { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerifyToken { get; set; }
    public bool IsAdmin { get; set; }
    public string Tier { get; set; } = "free";
    public int ReportsUsed { get; set; }
    public int PrototypesUsed { get; set; }
    public DateTime? ReportsResetAt { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
