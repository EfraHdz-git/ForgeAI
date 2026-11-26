namespace ForgeAI.Core.DTOs;

public class ManualIdeaDto
{
    public string Title { get; set; } = string.Empty;
    public string ProblemStatement { get; set; } = string.Empty;
    public string? TargetAudience { get; set; }
    public string? WhyNow { get; set; }
    public string? Platform { get; set; }
    public string? UserType { get; set; }
    public string? SimpleUseCase { get; set; }
    public string? FocusNiche { get; set; }
}
