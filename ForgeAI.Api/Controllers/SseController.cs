using ForgeAI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ForgeAI.Api.Controllers;

public class SseBroadcasterService : ISseBroadcaster
{
    public Task BroadcastAsync(string eventType, object data) => SseController.BroadcastAsync(eventType, data);
}

[ApiController]
[Route("api/[controller]")]
public class SseController : ControllerBase
{
    private static readonly ConcurrentBag<StreamWriter> _clients = new();

    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["Access-Control-Allow-Origin"] = "*";

        var writer = new StreamWriter(Response.Body);
        _clients.Add(writer);

        try
        {
            // Send initial heartbeat
            await writer.WriteLineAsync("event: connected");
            await writer.WriteLineAsync($"data: {{\"time\":\"{DateTime.UtcNow:O}\"}}");
            await writer.WriteLineAsync();
            await writer.FlushAsync();

            // Keep connection alive
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(30000, ct);
                await writer.WriteLineAsync(": heartbeat");
                await writer.FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Remove disconnected client
            var newBag = new ConcurrentBag<StreamWriter>(_clients.Where(c => c != writer));
            while (_clients.TryTake(out _)) { }
            foreach (var c in newBag) _clients.Add(c);
        }
    }

    public static async Task BroadcastAsync(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var deadClients = new List<StreamWriter>();

        foreach (var client in _clients)
        {
            try
            {
                await client.WriteLineAsync($"event: {eventType}");
                await client.WriteLineAsync($"data: {json}");
                await client.WriteLineAsync();
                await client.FlushAsync();
            }
            catch
            {
                deadClients.Add(client);
            }
        }

        // Clean up dead clients
        if (deadClients.Count > 0)
        {
            var newBag = new ConcurrentBag<StreamWriter>(_clients.Where(c => !deadClients.Contains(c)));
            while (_clients.TryTake(out _)) { }
            foreach (var c in newBag) _clients.Add(c);
        }
    }
}
