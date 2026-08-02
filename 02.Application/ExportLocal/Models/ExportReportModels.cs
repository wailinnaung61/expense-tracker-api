namespace expense_tracker_backend.Application.ExportLocal.Models;

public sealed class TransactionRow
{
    public string TransactionId { get; set; } = default!;
    public string Type { get; set; } = default!;           // INCOME, EXPENSE, INVESTMENT, SAVINGS
    public string CategoryId { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Description { get; set; } = default!;
    public string PaymentStatus { get; set; } = default!;  // PENDING, COMPLETED, FAILED
    public DateOnly TransactionDate { get; set; }
    public string Notes { get; set; } = default!;
}

public sealed class CategoryInfo
{
    public string CategoryId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Type { get; set; } = default!;
}

public sealed class BudgetCategoryRow
{
    public string CategoryId { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public decimal AllocatedAmount { get; set; }
    public DateOnly BudgetStart { get; set; }
    public DateOnly BudgetEnd { get; set; }
}

public sealed class UserProfile
{
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Currency { get; set; } = default!;
}
