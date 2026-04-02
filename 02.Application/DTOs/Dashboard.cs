namespace expense_tracker_backend.Application.DTOs;

public record DashboardResponse(
    MonthlyAggregation? CurrentMonth,
    List<MonthlyAggregation> MonthlyTrend,
    ExpenseBreakdown ExpenseBreakdown,
    IReadOnlyList<Tranaction> RecentTransactions,
    BudgetMonthlyResponse? Budget,
    SavingDashboardResponse Savings,
    InvestmentDashboardResponse Investment,
    List<RecurringPaymentDto> UpcomingBills
);
