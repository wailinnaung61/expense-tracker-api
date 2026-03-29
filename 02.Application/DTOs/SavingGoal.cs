namespace expense_tracker_backend.Application.DTOs;

public class SavingGoalDto
{
    public string SavingGoalId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string GoalName { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal InitialDeposit { get; set; }
    public DateTime TargetDate { get; set; }
    public string RecurringType { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
