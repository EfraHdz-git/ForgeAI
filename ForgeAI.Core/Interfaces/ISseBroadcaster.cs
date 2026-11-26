namespace ForgeAI.Core.Interfaces;

public interface ISseBroadcaster
{
    Task BroadcastAsync(string eventType, object data);
}
