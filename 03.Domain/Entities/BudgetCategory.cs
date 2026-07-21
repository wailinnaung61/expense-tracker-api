namespace expense_tracker_backend.Domain.Entities;

public class BudgetCategory
{
    public required string BudgetCategoryId { get; set; }
    public required string BudgetId { get; set; }
    public required string CategoryId { get; set; }
    public required decimal AllocatedAmount { get; set; }
    public decimal AlertThreshold { get; set; } = 0.8m;
    /// <summary>
    /// When true, unspent allocation is reserved from Daily Budget / spendable remaining
    /// (e.g. gas, rent not paid yet but already spoken for).
    /// </summary>
    public bool IsReserved { get; set; }
    /// <summary>
    /// When false, skip budget threshold/exceeded notifications for this category
    /// (e.g. fixed bills like electric where spend equals the allocation).
    /// </summary>
    public bool AlertsEnabled { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Budget? Budget { get; set; }
    public ExpenseCategory? Category { get; set; }
    public BudgetSnapshot? Snapshot { get; set; }
}
