namespace ForgeAI.Core.Models;

public class FocusProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? IncludedUsers { get; set; }
    public string? ExcludedUsers { get; set; }
    public string? IncludedProblems { get; set; }
    public string? ExcludedProblems { get; set; }
    public string? PlatformBias { get; set; }
    public string? MonetizationPref { get; set; }
    public string? ExampleUseCases { get; set; }
    public string? SourceConfig { get; set; }
    public DateTime CreatedAt { get; set; }
}
