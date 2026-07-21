using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface INotificationService
{
    // ── Query ──
    Task<NotificationSummary> GetSummaryAsync(Guid userId);
    Task<PagedNotificationResult> GetNotificationsAsync(Guid userId, bool? isRead, int pageSize, DateTime? cursor);
    Task<int> GetUnreadCountAsync(Guid userId);

    // ── Actions ──
    Task MarkAsReadAsync(Guid userId, Guid notificationId);
    Task MarkAllAsReadAsync(Guid userId);
    Task DeleteAsync(Guid userId, Guid notificationId);
    Task DeleteAllReadAsync(Guid userId);

    // ── Create (raw — called by other services / background jobs) ──
    Task SendAsync(Guid userId, string type, string title, string message,
        string? referenceId = null, string? referenceType = null,
        IReadOnlyDictionary<string, string>? emailPlaceholders = null,
        string? emailMilestone = null);
    Task SendBatchAsync(List<(Guid UserId, string Type, string Title, string Message,
        string? ReferenceId, string? ReferenceType)> notifications);

    // ── Create (localized — smart helpers) ──
    Task NotifyBudgetThresholdAsync(Guid userId, string categoryName, int percent,
        string spent, string allocated, string? budgetCategoryId = null);
    Task NotifyBudgetExceededAsync(Guid userId, string categoryName,
        string spent, string allocated, string? budgetCategoryId = null);
    Task NotifyRecurringDueAsync(Guid userId, string name, string amount,
        string dueDate, string? recurringId = null, string? milestone = null);
    Task NotifyRecurringOverdueAsync(Guid userId, string name, int missedCount,
        string? recurringId = null);
    Task NotifyRecurringAutoPaidAsync(Guid userId, string name, string amount,
        string? recurringId = null);
    Task NotifySavingGoalReachedAsync(Guid userId, string goalName,
        string? savingGoalId = null);
    Task NotifySavingGoalDeadlineAsync(Guid userId, string goalName, int daysLeft,
        string current, string target, string? savingGoalId = null);
    Task NotifyExportCompletedAsync(Guid userId, string startMonth, string endMonth,
        string? exportJobId = null);
    Task NotifyExportFailedAsync(Guid userId, string? exportJobId = null);
    Task NotifyLargeTransactionAsync(Guid userId, string amount, string description,
        string? transactionId = null);
    Task NotifyPaymentFailedAsync(Guid userId, string description, string amount,
        string? transactionId = null);
}
