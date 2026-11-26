namespace ForgeAI.Core.Models;

public class ScoutSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int PollingIntervalMinutes { get; set; } = 360;
    public string? ParserType { get; set; }
    public string? AuthConfig { get; set; }
    public int RateLimit { get; set; } = 60;
    public DateTime? LastRunAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
