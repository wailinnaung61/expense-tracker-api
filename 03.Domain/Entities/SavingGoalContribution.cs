using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class SavingGoalContribution
{
    public required string ContributionId { get; set; }
    public required string SavingGoalId { get; set; }
    public required string UserId { get; set; }
    public AppConstants.SavingTransactionType Type { get; set; } = AppConstants.SavingTransactionType.Deposit;
    public required decimal Amount { get; set; }
    public required string ContributionDate { get; set; }  // yyyy-MM-dd
    public string Notes { get; set; } = string.Empty;
    // Links to the mirrored row in transactions table
    public string? MirrorTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public SavingGoal? SavingGoal { get; set; }
}
