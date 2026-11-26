namespace ForgeAI.Core.Models;

public class RawSignal
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? PainPoint { get; set; }
    public int FrequencyScore { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public bool IsClustered { get; set; }
    public int? ClusterId { get; set; }
}
