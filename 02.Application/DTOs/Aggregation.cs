namespace expense_tracker_backend.Application.DTOs;

public record DailyAggregation(
    string Period,
    decimal Income,
    decimal Expense,
    decimal Saving,
    decimal Investment,
    int TransactionCount
);

public record WeeklyAggregation(
    string Period,
    decimal Income,
    decimal Expense,
    decimal Saving,
    decimal Investment,
    int TransactionCount
);

public record MonthlyAggregation(
    string Period,
    string PeriodStart,
    string PeriodEnd,
    decimal Income,
    decimal Expense,
    decimal Saving,
    decimal Investment,
    int TransactionCount
);

public record YearlyAggregation(
    string Period,
    decimal Income,
    decimal Expense,
    decimal Saving,
    decimal Investment,
    int TransactionCount
);

public record CategoryMonthlyAggregation(
    string CategoryId,
    string Period,
    string PeriodStart,
    string PeriodEnd,
    decimal TotalAmount,
    int TransactionCount
);

public record ExpenseBreakdown(
    decimal TotalExpenses,
    List<CategoryBreakdownItem> Categories,
    MonthlyComparison? Comparison
);

public record CategoryBreakdownItem(
    string CategoryId,
    string CategoryName,
    decimal Amount,
    double Percentage
);

public record MonthlyComparison(
    decimal LastMonth,
    decimal ThisMonth,
    decimal Difference,
    double PercentageChange
);
