using Dapper;
using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;

namespace ForgeAI.Data.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly DbConnectionFactory _db;

    public NotificationRepository(DbConnectionFactory db) => _db = db;

    public async Task<int> CreateAsync(Notification notification)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(@"
            INSERT INTO notifications (type, title, body, review_url, status)
            OUTPUT INSERTED.id
            VALUES (@Type, @Title, @Body, @ReviewUrl, @Status)", notification);
    }

    public async Task<IEnumerable<Notification>> GetUnreadAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Notification>(
            "SELECT * FROM notifications WHERE read_at IS NULL ORDER BY sent_at DESC");
    }

    public async Task MarkReadAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE notifications SET read_at = GETUTCDATE(), status = 'read' WHERE id = @Id",
            new { Id = id });
    }
}
