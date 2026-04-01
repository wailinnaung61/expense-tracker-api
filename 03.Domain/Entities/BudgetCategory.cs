namespace expense_tracker_backend.Domain.Entities;

public class BudgetCategory
{
    public required string BudgetCategoryId { get; set; }
    public required string BudgetId { get; set; }
    public required string CategoryId { get; set; }
    public required decimal AllocatedAmount { get; set; }
    public decimal AlertThreshold { get; set; } = 0.8m;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Budget? Budget { get; set; }
    public ExpenseCategory? Category { get; set; }
    public BudgetSnapshot? Snapshot { get; set; }
}
