using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class AggregationChatHandler
{
    private readonly IAggregationService _aggregationService;
    private readonly IDashboardService _dashboardService;

    public AggregationChatHandler(
        IAggregationService aggregationService,
        IDashboardService dashboardService)
    {
        _aggregationService = aggregationService;
        _dashboardService = dashboardService;
    }

    public async Task<(string, object?)> GetMonthlySummaryAsync(Guid userId, JsonElement args)
    {
        var month = args.TryGetProperty("month", out var m)
            ? m.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM")
            : DateTime.UtcNow.ToString("yyyy-MM");

        var result = await _aggregationService.GetMonthlyAggregationAsync(userId, month);

        if (result is null)
            return ($"No data found for {month}.", null);

        var net = result.Income - result.Expense - result.Saving - result.Investment;
        var summary = $"Monthly Summary for {month}:\n" +
            $"• Income: {result.Income:N0}\n" +
            $"• Expense: {result.Expense:N0}\n" +
            $"• Savings: {result.Saving:N0}\n" +
            $"• Investment: {result.Investment:N0}\n" +
            $"• Net: {net:N0}\n" +
            $"• Transactions: {result.TransactionCount}";

        return (summary, result);
    }

    public async Task<(string, object?)> GetYearlySummaryAsync(Guid userId, JsonElement args)
    {
        var year = args.TryGetProperty("year", out var y)
            ? y.GetString() ?? DateTime.UtcNow.ToString("yyyy")
            : DateTime.UtcNow.ToString("yyyy");

        var result = await _aggregationService.GetYearlyAggregationAsync(userId, year);

        if (result is null)
            return ($"No data found for {year}.", null);

        var summary = $"Yearly Summary for {year}:\n" +
            $"• Income: {result.Income:N0}\n" +
            $"• Expense: {result.Expense:N0}\n" +
            $"• Savings: {result.Saving:N0}\n" +
            $"• Investment: {result.Investment:N0}\n" +
            $"• Transactions: {result.TransactionCount}";

        return (summary, result);
    }

    public async Task<(string, object?)> GetExpenseBreakdownAsync(Guid userId, JsonElement args)
    {
        var month = args.TryGetProperty("month", out var m)
            ? m.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM")
            : DateTime.UtcNow.ToString("yyyy-MM");

        var result = await _aggregationService.GetExpenseBreakdownAsync(userId, month);

        if (result.Categories.Count == 0)
            return ($"No expense data found for {month}.", result);

        var lines = result.Categories.Select(c =>
            $"• {c.CategoryName}: {c.Amount:N0} ({c.Percentage:F1}%)");
        var summary = $"Expense Breakdown for {month} (Total: {result.TotalExpenses:N0}):\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> GetDashboardAsync(Guid userId, JsonElement args)
    {
        var month = args.TryGetProperty("month", out var m)
            ? m.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM")
            : DateTime.UtcNow.ToString("yyyy-MM");

        var result = await _dashboardService.GetDashboardAsync(userId, month);

        var parts = new List<string> { $"Dashboard for {month}:" };

        if (result.CurrentMonth is not null)
        {
            var cm = result.CurrentMonth;
            parts.Add($"Income: {cm.Income:N0} | Expense: {cm.Expense:N0} | Savings: {cm.Saving:N0} | Investment: {cm.Investment:N0}");
        }

        if (result.Budget?.Summary is not null)
        {
            var b = result.Budget.Summary;
            parts.Add($"Budget: {b.TotalSpent:N0}/{b.TotalBudget:N0} ({b.UsagePercent}% used)");
        }

        parts.Add($"Savings: {result.Savings.TotalSaved:N0} saved, {result.Savings.ActiveGoalsCount} active goals");
        parts.Add($"Investments: {result.Investment.TotalInvested:N0} invested, P/L: {result.Investment.TotalProfitLoss:N0}");

        if (result.UpcomingBills.Count > 0)
        {
            var bills = string.Join(", ", result.UpcomingBills.Take(3).Select(b => $"{b.Name} ({b.Amount:N0})"));
            parts.Add($"Upcoming bills: {bills}");
        }

        return (string.Join("\n", parts), result);
    }
}
