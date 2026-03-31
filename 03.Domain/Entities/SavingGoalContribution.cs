namespace expense_tracker_backend.Domain.Entities;

public class SavingGoalContribution
{
    public required string ContributionId { get; set; }
    public required string SavingGoalId { get; set; }
    public required string UserId { get; set; }
    public required decimal Amount { get; set; }
    public DateTime ContributionDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public SavingGoal? SavingGoal { get; set; }
}
