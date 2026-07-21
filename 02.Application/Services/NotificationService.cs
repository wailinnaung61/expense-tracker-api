using System.Globalization;
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
    private readonly IEmailNotificationService _emailNotification;
    private readonly IStringLocalizer _localizer;

    public NotificationService(
        INotificationRepository repository,
        IMemberRepository memberRepository,
        IEmailNotificationService emailNotification,
        IStringLocalizer localizer)
    {
        _repository = repository;
        _memberRepository = memberRepository;
        _emailNotification = emailNotification;
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
        string? referenceId = null, string? referenceType = null,
        IReadOnlyDictionary<string, string>? emailPlaceholders = null,
        string? emailMilestone = null)
    {
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

        if (emailPlaceholders is not null)
        {
            try
            {
                await _emailNotification.TrySendAsync(
                    userId, type, emailPlaceholders, referenceId, emailMilestone);
            }
            catch (Exception)
            {
                // Email failures must not break in-app notifications
            }
        }
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
        if (profile is null) return true;

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

    private string Localize(string locale, string key, params object[] args)
    {
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(locale);
            var value = _localizer[key].Value;
            return args.Length > 0 ? string.Format(value, args) : value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }

    private async Task<string> GetUserLocaleAsync(Guid userId)
    {
        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());
        return profile?.Locale ?? "en";
    }

    // ── Localized notification helpers ──

    public async Task NotifyBudgetThresholdAsync(Guid userId, string categoryName, int percent,
        string spent, string allocated, string? budgetCategoryId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.BudgetThresholdReached,
            Localize(loc, "Notif_BudgetThreshold_Title"),
            Localize(loc, "Notif_BudgetThreshold_Msg", percent, categoryName, spent, allocated),
            budgetCategoryId, "budget",
            new Dictionary<string, string>
            {
                ["categoryName"] = categoryName,
                ["percent"] = percent.ToString(),
                ["spent"] = spent,
                ["allocated"] = allocated
            });
    }

    public async Task NotifyBudgetExceededAsync(Guid userId, string categoryName,
        string spent, string allocated, string? budgetCategoryId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.BudgetExceeded,
            Localize(loc, "Notif_BudgetExceeded_Title"),
            Localize(loc, "Notif_BudgetExceeded_Msg", categoryName, spent, allocated),
            budgetCategoryId, "budget",
            new Dictionary<string, string>
            {
                ["categoryName"] = categoryName,
                ["spent"] = spent,
                ["allocated"] = allocated
            });
    }

    public async Task NotifyRecurringDueAsync(Guid userId, string name, string amount,
        string dueDate, string? recurringId = null, string? milestone = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.RecurringPaymentDue,
            Localize(loc, "Notif_RecurringDue_Title"),
            Localize(loc, "Notif_RecurringDue_Msg", name, amount, dueDate),
            recurringId, "recurring_payment",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["amount"] = amount,
                ["dueDate"] = dueDate
            },
            milestone);
    }

    public async Task NotifyRecurringOverdueAsync(Guid userId, string name, int missedCount,
        string? recurringId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.RecurringPaymentOverdue,
            Localize(loc, "Notif_RecurringOverdue_Title"),
            Localize(loc, "Notif_RecurringOverdue_Msg", name, missedCount),
            recurringId, "recurring_payment",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["missedCount"] = missedCount.ToString()
            },
            emailMilestone: $"overdue_{missedCount}");
    }

    public async Task NotifyRecurringAutoPaidAsync(Guid userId, string name, string amount,
        string? recurringId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.RecurringPaymentAutoPaid,
            Localize(loc, "Notif_RecurringAutoPaid_Title"),
            Localize(loc, "Notif_RecurringAutoPaid_Msg", name, amount),
            recurringId, "recurring_payment",
            new Dictionary<string, string>
            {
                ["name"] = name,
                ["amount"] = amount
            });
    }

    public async Task NotifySavingGoalReachedAsync(Guid userId, string goalName,
        string? savingGoalId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.SavingGoalReached,
            Localize(loc, "Notif_SavingGoalReached_Title"),
            Localize(loc, "Notif_SavingGoalReached_Msg", goalName),
            savingGoalId, "saving_goal",
            new Dictionary<string, string> { ["goalName"] = goalName });
    }

    public async Task NotifySavingGoalDeadlineAsync(Guid userId, string goalName, int daysLeft,
        string current, string target, string? savingGoalId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.SavingGoalDeadlineNear,
            Localize(loc, "Notif_SavingGoalDeadline_Title"),
            Localize(loc, "Notif_SavingGoalDeadline_Msg", goalName, daysLeft, current, target),
            savingGoalId, "saving_goal",
            new Dictionary<string, string>
            {
                ["goalName"] = goalName,
                ["daysLeft"] = daysLeft.ToString(),
                ["current"] = current,
                ["target"] = target
            },
            emailMilestone: $"deadline_{daysLeft}");
    }

    public async Task NotifyExportCompletedAsync(Guid userId, string startMonth, string endMonth,
        string? exportJobId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.ExportCompleted,
            Localize(loc, "Notif_ExportCompleted_Title"),
            Localize(loc, "Notif_ExportCompleted_Msg", startMonth, endMonth),
            exportJobId, "export",
            new Dictionary<string, string>
            {
                ["startMonth"] = startMonth,
                ["endMonth"] = endMonth
            });
    }

    public async Task NotifyExportFailedAsync(Guid userId, string? exportJobId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.ExportFailed,
            Localize(loc, "Notif_ExportFailed_Title"),
            Localize(loc, "Notif_ExportFailed_Msg"),
            exportJobId, "export",
            new Dictionary<string, string>());
    }

    public async Task NotifyLargeTransactionAsync(Guid userId, string amount, string description,
        string? transactionId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.LargeTransaction,
            Localize(loc, "Notif_LargeTransaction_Title"),
            Localize(loc, "Notif_LargeTransaction_Msg", amount, description),
            transactionId, "transaction",
            new Dictionary<string, string>
            {
                ["amount"] = amount,
                ["description"] = description
            });
    }

    public async Task NotifyPaymentFailedAsync(Guid userId, string description, string amount,
        string? transactionId = null)
    {
        var loc = await GetUserLocaleAsync(userId);
        await SendAsync(userId, NotificationType.PaymentFailed,
            Localize(loc, "Notif_PaymentFailed_Title"),
            Localize(loc, "Notif_PaymentFailed_Msg", description, amount),
            transactionId, "transaction",
            new Dictionary<string, string>
            {
                ["description"] = description,
                ["amount"] = amount
            });
    }
}
