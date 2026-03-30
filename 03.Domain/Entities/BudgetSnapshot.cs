namespace expense_tracker_backend.Domain.Entities;

public class BudgetSnapshot
{
    public required string BudgetCategoryId { get; set; }
    public decimal SpentAmount { get; set; } = 0;
    public int TransactionCount { get; set; } = 0;
    public string? LastTransactionDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public BudgetCategory? BudgetCategory { get; set; }
}
