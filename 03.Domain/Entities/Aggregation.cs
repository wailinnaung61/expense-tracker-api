namespace expense_tracker_backend.Domain.Entities;

public class Aggregation
{
    public string Period { get; set; } = string.Empty;
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Saving { get; set; }
    public decimal Investment { get; set; }
    public int TransactionCount { get; set; }
}

public class CategoryAggregation
{
    public string CategoryId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}
