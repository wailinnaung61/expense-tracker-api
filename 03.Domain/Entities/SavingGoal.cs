using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class SavingGoal
{
    public required string SavingGoalId { get; set; }
    public required string UserId { get; set; }
    public required string GoalName { get; set; }
    public string Description { get; set; } = string.Empty;
    public required decimal TargetAmount { get; set; }
    // Persisted running total — updated on every deposit/withdrawal
    public decimal CurrentAmount { get; set; } = 0;
    public required string TargetDate { get; set; }  // yyyy-MM-dd
    public AppConstants.SavingGoalStatus Status { get; set; } = AppConstants.SavingGoalStatus.Active;
    public string Notes { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public MemberProfile? User { get; set; }
    public ICollection<SavingGoalContribution> Contributions { get; set; } = [];
}
