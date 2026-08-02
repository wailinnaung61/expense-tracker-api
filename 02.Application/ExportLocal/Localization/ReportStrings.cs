namespace expense_tracker_backend.Application.ExportLocal.Localization;

/// <summary>
/// All UI strings used in the Excel report, resolved by locale.
/// </summary>
public sealed class ReportStrings
{
    // ── Report Title / Header
    public string ReportTitle { get; init; } = default!;
    public string UserLabel { get; init; } = default!;
    public string PeriodLabel { get; init; } = default!;
    public string CurrencyLabel { get; init; } = default!;

    // ── Summary sheet sections
    public string Summary { get; init; } = default!;
    public string MonthlyOverview { get; init; } = default!;
    public string IncomeBreakdown { get; init; } = default!;
    public string ExpenseBreakdown { get; init; } = default!;
    public string TransactionStatusSummary { get; init; } = default!;
    public string BalanceSummary { get; init; } = default!;

    // ── Column headers
    public string Month { get; init; } = default!;
    public string Income { get; init; } = default!;
    public string Expense { get; init; } = default!;
    public string Investment { get; init; } = default!;
    public string Savings { get; init; } = default!;
    public string NetCashFlow { get; init; } = default!;
    public string Difference { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Budgeted { get; init; } = default!;
    public string ActualSpent { get; init; } = default!;
    public string PercentUsed { get; init; } = default!;
    public string Amount { get; init; } = default!;
    public string PercentOfTotalIncome { get; init; } = default!;
    public string PercentOfTotalExpense { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string Count { get; init; } = default!;
    public string TotalAmount { get; init; } = default!;
    public string Date { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Notes { get; init; } = default!;

    // ── Row labels
    public string Total { get; init; } = default!;
    public string Subtotal { get; init; } = default!;
    public string TotalMonthlyIncome { get; init; } = default!;
    public string TotalExpense { get; init; } = default!;
    public string MonthlySummary { get; init; } = default!;
    public string BudgetVsActual { get; init; } = default!;
    public string Transactions { get; init; } = default!;
    public string TransactionsLabel { get; init; } = default!; // "{TYPE} Transactions"
    public string SubtotalWithCount { get; init; } = default!; // "Subtotal ({0} transactions)"

    // ── Balance summary labels
    public string TotalIncomeCompleted { get; init; } = default!;
    public string TotalExpensesCompleted { get; init; } = default!;
    public string TotalInvestmentsCompleted { get; init; } = default!;
    public string TotalSavingsCompleted { get; init; } = default!;

    // ── Payment statuses (display)
    public string Completed { get; init; } = default!;
    public string Pending { get; init; } = default!;
    public string Failed { get; init; } = default!;

    // ── Averages & Projections
    public string AveragesAndProjections { get; init; } = default!;
    public string Metric { get; init; } = default!;
    public string Value { get; init; } = default!;
    public string AvgDailySpending { get; init; } = default!;
    public string AvgWeeklySpending { get; init; } = default!;
    public string AvgMonthlySpending { get; init; } = default!;
    public string YearlyProjectionExpense { get; init; } = default!;
    public string AvgDailyIncome { get; init; } = default!;
    public string AvgMonthlyIncome { get; init; } = default!;
    public string YearlyProjectionIncome { get; init; } = default!;
    public string ProjectedYearlySavings { get; init; } = default!;
}
