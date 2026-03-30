using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class Budget
{
    public required string BudgetId { get; set; }
    public required string UserId { get; set; }
    public AppConstants.BudgetPeriodType PeriodType { get; set; } = AppConstants.BudgetPeriodType.Monthly;
    public required string StartDate { get; set; }
    public required string EndDate { get; set; }
    public required decimal TotalAmount { get; set; }
    public AppConstants.BudgetStatus Status { get; set; } = AppConstants.BudgetStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public MemberProfile? User { get; set; }
    public ICollection<BudgetCategory> BudgetCategories { get; set; } = [];
}
