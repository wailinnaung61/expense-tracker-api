using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class RecurringPayment
{
    public required string RecurringId { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required decimal Amount { get; set; }
    public required string CategoryId { get; set; }
    public required AppConstants.RecurringFrequency Frequency { get; set; }
    public required DateTime NextDueDate { get; set; }
    public DateTime? LastPaidDate { get; set; }
    public int MissedCount { get; set; } = 0;
    public AppConstants.RecurringStatus Status { get; set; } = AppConstants.RecurringStatus.Active;
    public bool AutoPay { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public MemberProfile? User { get; set; }
    public ExpenseCategory? Category { get; set; }
}
