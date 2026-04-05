namespace expense_tracker_backend.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;         // BUDGET_ALERT, RECURRING_DUE, etc.
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }                  // e.g., budgetId, recurringId, savingGoalId
    public string? ReferenceType { get; set; }                // e.g., "budget", "recurring_payment", "saving_goal"
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public static class NotificationType
{
    // Budget
    public const string BudgetThresholdReached = "BUDGET_THRESHOLD_REACHED";
    public const string BudgetExceeded = "BUDGET_EXCEEDED";

    // Recurring Payments
    public const string RecurringPaymentDue = "RECURRING_PAYMENT_DUE";
    public const string RecurringPaymentOverdue = "RECURRING_PAYMENT_OVERDUE";
    public const string RecurringPaymentAutoPaid = "RECURRING_PAYMENT_AUTO_PAID";

    // Saving Goals
    public const string SavingGoalReached = "SAVING_GOAL_REACHED";
    public const string SavingGoalDeadlineNear = "SAVING_GOAL_DEADLINE_NEAR";

    // Export
    public const string ExportCompleted = "EXPORT_COMPLETED";
    public const string ExportFailed = "EXPORT_FAILED";

    // General
    public const string LargeTransaction = "LARGE_TRANSACTION";
    public const string PaymentFailed = "PAYMENT_FAILED";
}
