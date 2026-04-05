using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Notification> CreateAsync(Notification notification);
    Task CreateBatchAsync(List<Notification> notifications);
    Task<(List<Notification> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId, bool? isRead, int pageSize, DateTime? cursor);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
    Task DeleteAsync(Guid notificationId, Guid userId);
    Task DeleteAllReadAsync(Guid userId);
}
