using _02.Application;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly IMemberRepository _memberRepository;
    private readonly IStringLocalizer<ApplicationResource> _localizer;

    public NotificationService(
        INotificationRepository repository,
        IMemberRepository memberRepository,
        IStringLocalizer<ApplicationResource> localizer)
    {
        _repository = repository;
        _memberRepository = memberRepository;
        _localizer = localizer;
    }

    // ── Query ──

    public async Task<NotificationSummary> GetSummaryAsync(Guid userId)
    {
        var unreadCount = await _repository.GetUnreadCountAsync(userId);
        var (items, _) = await _repository.GetByUserIdAsync(userId, null, 5, null);
        return new NotificationSummary(unreadCount, items.Select(MapToDto).ToList());
    }

    public async Task<PagedNotificationResult> GetNotificationsAsync(
        Guid userId, bool? isRead, int pageSize, DateTime? cursor)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (items, totalCount) = await _repository.GetByUserIdAsync(userId, isRead, pageSize + 1, cursor);

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedNotificationResult(
            resultItems.Select(MapToDto).ToList(),
            totalCount,
            hasNextPage,
            lastItem?.CreatedAt
        );
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _repository.GetUnreadCountAsync(userId);
    }

    // ── Actions ──

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        await _repository.MarkAsReadAsync(notificationId, userId);
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _repository.MarkAllAsReadAsync(userId);
    }

    public async Task DeleteAsync(Guid userId, Guid notificationId)
    {
        await _repository.DeleteAsync(notificationId, userId);
    }

    public async Task DeleteAllReadAsync(Guid userId)
    {
        await _repository.DeleteAllReadAsync(userId);
    }

    // ── Create ──

    public async Task SendAsync(Guid userId, string type, string title, string message,
        string? referenceId = null, string? referenceType = null)
    {
        // Check user's notification preferences
        if (!await IsNotificationEnabledAsync(userId, type)) return;

        var notification = new Notification
        {
            UserId = userId.ToString(),
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
            ReferenceType = referenceType
        };
        await _repository.CreateAsync(notification);
    }

    public async Task SendBatchAsync(List<(Guid UserId, string Type, string Title, string Message,
        string? ReferenceId, string? ReferenceType)> notifications)
    {
        var entities = notifications.Select(n => new Notification
        {
            UserId = n.UserId.ToString(),
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            ReferenceId = n.ReferenceId,
            ReferenceType = n.ReferenceType
        }).ToList();

        await _repository.CreateBatchAsync(entities);
    }

    private async Task<bool> IsNotificationEnabledAsync(Guid userId, string type)
    {
        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());
        if (profile is null) return true; // no profile = allow all

        return type switch
        {
            NotificationType.BudgetThresholdReached or
            NotificationType.BudgetExceeded => profile.NotifyBudgetAlerts,

            NotificationType.RecurringPaymentDue or
            NotificationType.RecurringPaymentOverdue => profile.NotifyRecurringPayments,

            NotificationType.RecurringPaymentAutoPaid => profile.NotifyAutoPayments,

            NotificationType.SavingGoalReached or
            NotificationType.SavingGoalDeadlineNear => profile.NotifySavingGoals,

            NotificationType.LargeTransaction => profile.NotifyLargeTransactions,

            NotificationType.PaymentFailed => profile.NotifyPaymentFailures,

            NotificationType.ExportCompleted or
            NotificationType.ExportFailed => profile.NotifyExports,

            _ => true
        };
    }

    private static NotificationDto MapToDto(Notification n) =>
        new(n.Id, n.Type, n.Title, n.Message, n.ReferenceId, n.ReferenceType,
            n.IsRead, n.CreatedAt, n.ReadAt);

    // ── Localized notification helpers ──

    public Task NotifyBudgetThresholdAsync(Guid userId, string categoryName, int percent,
        string spent, string allocated, string? budgetCategoryId = null) =>
        SendAsync(userId, NotificationType.BudgetThresholdReached,
            _localizer["Notif_BudgetThreshold_Title"],
            string.Format(_localizer["Notif_BudgetThreshold_Msg"], percent, categoryName, spent, allocated),
            budgetCategoryId, "budget");

    public Task NotifyBudgetExceededAsync(Guid userId, string categoryName,
        string spent, string allocated, string? budgetCategoryId = null) =>
        SendAsync(userId, NotificationType.BudgetExceeded,
            _localizer["Notif_BudgetExceeded_Title"],
            string.Format(_localizer["Notif_BudgetExceeded_Msg"], categoryName, spent, allocated),
            budgetCategoryId, "budget");

    public Task NotifyRecurringDueAsync(Guid userId, string name, string amount,
        string dueDate, string? recurringId = null) =>
        SendAsync(userId, NotificationType.RecurringPaymentDue,
            _localizer["Notif_RecurringDue_Title"],
            string.Format(_localizer["Notif_RecurringDue_Msg"], name, amount, dueDate),
            recurringId, "recurring_payment");

    public Task NotifyRecurringOverdueAsync(Guid userId, string name, int missedCount,
        string? recurringId = null) =>
        SendAsync(userId, NotificationType.RecurringPaymentOverdue,
            _localizer["Notif_RecurringOverdue_Title"],
            string.Format(_localizer["Notif_RecurringOverdue_Msg"], name, missedCount),
            recurringId, "recurring_payment");

    public Task NotifyRecurringAutoPaidAsync(Guid userId, string name, string amount,
        string? recurringId = null) =>
        SendAsync(userId, NotificationType.RecurringPaymentAutoPaid,
            _localizer["Notif_RecurringAutoPaid_Title"],
            string.Format(_localizer["Notif_RecurringAutoPaid_Msg"], name, amount),
            recurringId, "recurring_payment");

    public Task NotifySavingGoalReachedAsync(Guid userId, string goalName,
        string? savingGoalId = null) =>
        SendAsync(userId, NotificationType.SavingGoalReached,
            _localizer["Notif_SavingGoalReached_Title"],
            string.Format(_localizer["Notif_SavingGoalReached_Msg"], goalName),
            savingGoalId, "saving_goal");

    public Task NotifySavingGoalDeadlineAsync(Guid userId, string goalName, int daysLeft,
        string current, string target, string? savingGoalId = null) =>
        SendAsync(userId, NotificationType.SavingGoalDeadlineNear,
            _localizer["Notif_SavingGoalDeadline_Title"],
            string.Format(_localizer["Notif_SavingGoalDeadline_Msg"], goalName, daysLeft, current, target),
            savingGoalId, "saving_goal");

    public Task NotifyExportCompletedAsync(Guid userId, string startMonth, string endMonth,
        string? exportJobId = null) =>
        SendAsync(userId, NotificationType.ExportCompleted,
            _localizer["Notif_ExportCompleted_Title"],
            string.Format(_localizer["Notif_ExportCompleted_Msg"], startMonth, endMonth),
            exportJobId, "export");

    public Task NotifyExportFailedAsync(Guid userId, string? exportJobId = null) =>
        SendAsync(userId, NotificationType.ExportFailed,
            _localizer["Notif_ExportFailed_Title"],
            _localizer["Notif_ExportFailed_Msg"],
            exportJobId, "export");

    public Task NotifyLargeTransactionAsync(Guid userId, string amount, string description,
        string? transactionId = null) =>
        SendAsync(userId, NotificationType.LargeTransaction,
            _localizer["Notif_LargeTransaction_Title"],
            string.Format(_localizer["Notif_LargeTransaction_Msg"], amount, description),
            transactionId, "transaction");

    public Task NotifyPaymentFailedAsync(Guid userId, string description, string amount,
        string? transactionId = null) =>
        SendAsync(userId, NotificationType.PaymentFailed,
            _localizer["Notif_PaymentFailed_Title"],
            string.Format(_localizer["Notif_PaymentFailed_Msg"], description, amount),
            transactionId, "transaction");
}
