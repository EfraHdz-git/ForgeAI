using ForgeAI.Core.Models;

namespace ForgeAI.Core.Interfaces;

public interface INotificationRepository
{
    Task<int> CreateAsync(Notification notification);
    Task<IEnumerable<Notification>> GetUnreadAsync();
    Task MarkReadAsync(int id);
}
