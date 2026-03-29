using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class SavingGoal
{
    public required string SavingGoalId { get; set; }
    public required string UserId { get; set; }
    public required string CategoryId { get; set; }
    public required string GoalName { get; set; }
    public required decimal TargetAmount { get; set; }
    public decimal InitialDeposit { get; set; }
    public required DateTime TargetDate { get; set; }
    public AppConstants.RecurringFrequency RecurringType { get; set; } = AppConstants.RecurringFrequency.Monthly;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public AppConstants.RecurringStatus Status { get; set; } = AppConstants.RecurringStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public MemberProfile? User { get; set; }
    public ExpenseCategory? Category { get; set; }
}
