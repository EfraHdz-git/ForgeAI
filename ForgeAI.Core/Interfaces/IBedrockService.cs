namespace ForgeAI.Core.Interfaces;

public interface IBedrockService
{
    Task<string> InvokeAsync(string prompt, CancellationToken ct = default);
}
