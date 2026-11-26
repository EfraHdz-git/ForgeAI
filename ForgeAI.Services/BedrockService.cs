using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using ForgeAI.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services;

public class BedrockService : IBedrockService
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly string _modelId;
    private readonly ILogger<BedrockService> _logger;

    public BedrockService(IConfiguration config, ILogger<BedrockService> logger)
    {
        _logger = logger;

        var region = Amazon.RegionEndpoint.GetBySystemName(
            config["AWS:Region"] ?? "us-east-2");
        _client = new AmazonBedrockRuntimeClient(region);
        _modelId = config["AWS:Bedrock:ModelId"] ?? "us.anthropic.claude-sonnet-5-5";
    }

    public async Task<string> InvokeAsync(string prompt, CancellationToken ct = default)
    {
        _logger.LogDebug("Bedrock invoke: {ModelId}, prompt length: {Length}", _modelId, prompt.Length);

        var payload = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 4096,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var request = new InvokeModelRequest
        {
            ModelId = _modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))
        };

        var response = await _client.InvokeModelAsync(request, ct);

        using var reader = new StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync(ct);

        // Newer models (Sonnet 5.5+) may return a "thinking" block before the answer, so join only the text blocks.
        using var doc = JsonDocument.Parse(responseJson);
        var content = new StringBuilder();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                && block.TryGetProperty("text", out var text))
                content.Append(text.GetString());
        }

        _logger.LogDebug("Bedrock response length: {Length}", content.Length);
        return content.ToString();
    }
}
