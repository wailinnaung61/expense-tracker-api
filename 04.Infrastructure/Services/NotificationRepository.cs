using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context) => _context = context;

    public async Task<Notification> CreateAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task CreateBatchAsync(List<Notification> notifications)
    {
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId, bool? isRead, int pageSize, DateTime? cursor)
    {
        var uid = userId.ToString();
        var query = _context.Notifications.Where(n => n.UserId == uid);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var totalCount = await query.CountAsync();

        if (cursor.HasValue)
            query = query.Where(n => n.CreatedAt < cursor.Value);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        var uid = userId.ToString();
        return await _context.Notifications
            .CountAsync(n => n.UserId == uid && !n.IsRead);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var uid = userId.ToString();
        await _context.Notifications
            .Where(n => n.Id == notificationId && n.UserId == uid && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var uid = userId.ToString();
        await _context.Notifications
            .Where(n => n.UserId == uid && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    public async Task DeleteAsync(Guid notificationId, Guid userId)
    {
        var uid = userId.ToString();
        await _context.Notifications
            .Where(n => n.Id == notificationId && n.UserId == uid)
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAllReadAsync(Guid userId)
    {
        var uid = userId.ToString();
        await _context.Notifications
            .Where(n => n.UserId == uid && n.IsRead)
            .ExecuteDeleteAsync();
    }
}
