using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class BudgetChatHandler
{
    private readonly IBudgetService _budgetService;
    private readonly IExpenseCategoryService _categoryService;

    public BudgetChatHandler(IBudgetService budgetService, IExpenseCategoryService categoryService)
    {
        _budgetService = budgetService;
        _categoryService = categoryService;
    }

    public async Task<(string, object?)> GetBudgetAsync(Guid userId, JsonElement args)
    {
        var year = TryInt(args, "year", DateTime.UtcNow.Year);
        var month = TryInt(args, "month", DateTime.UtcNow.Month);

        var result = await _budgetService.GetByMonthAsync(userId, year, month);

        if (result is null)
            return ($"No budget found for {year}-{month:D2}. You can create one!", null);

        var lines = result.Categories.Select(c =>
            $"• {c.Name} {c.Icon}: {c.Spent:N0}/{c.Allocated:N0} ({c.UsagePercent}%)");

        var summary = $"Budget for {year}-{month:D2}:\n" +
            $"Total: {result.Summary.TotalBudget:N0} | Spent: {result.Summary.TotalSpent:N0} | " +
            $"Remaining: {result.Summary.Remaining:N0} | Reserved: {result.Summary.ReservedRemaining:N0} | " +
            $"Spendable: {result.Summary.SpendableRemaining:N0} | Daily: {result.Summary.DailyBudget:N0} " +
            $"({result.Summary.UsagePercent}% used)\n" +
            string.Join("\n", lines);

        return (summary, result);
    }

    public async Task<(string, object?)> GetBudgetByRangeAsync(Guid userId, JsonElement args)
    {
        var start = TryStr(args, "start_date");
        var end = TryStr(args, "end_date");
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            return ("Please provide start_date and end_date (yyyy-MM-dd).", null);

        if (!DateOnly.TryParse(start, out var startD) || !DateOnly.TryParse(end, out var endD))
            return ("Invalid date format. Use yyyy-MM-dd.", null);

        if (startD > endD)
            return ("start_date must be on or before end_date.", null);

        var result = await _budgetService.GetByDateRangeAsync(userId, start, end);

        if (result is null)
            return ($"No budget found overlapping {start}–{end}.", null);

        var lines = result.Categories.Select(c =>
            $"• {c.Name} {c.Icon}: {c.Spent:N0}/{c.Allocated:N0} ({c.UsagePercent}%)");

        var summary = $"Budget {result.StartDate} → {result.EndDate}:\n" +
            $"Total: {result.Summary.TotalBudget:N0} | Spent: {result.Summary.TotalSpent:N0} | " +
            $"Remaining: {result.Summary.Remaining:N0} | Reserved: {result.Summary.ReservedRemaining:N0} | " +
            $"Spendable: {result.Summary.SpendableRemaining:N0} | Daily: {result.Summary.DailyBudget:N0} " +
            $"({result.Summary.UsagePercent}% used)\n" +
            string.Join("\n", lines);

        return (summary, result);
    }

    public async Task<(string, object?)> GetBudgetContainingDateAsync(Guid userId, JsonElement args)
    {
        var date = TryStr(args, "date");
        if (string.IsNullOrWhiteSpace(date))
            return ("Please provide date (yyyy-MM-dd) — the day that must fall inside the budget period (e.g. pay-cycle).", null);

        if (!DateOnly.TryParse(date, out _))
            return ("Invalid date format. Use yyyy-MM-dd.", null);

        var result = await _budgetService.GetByContainingDateAsync(userId, date);

        if (result is null)
            return ($"No budget contains {date}.", null);

        var lines = result.Categories.Select(c =>
            $"• {c.Name} {c.Icon}: {c.Spent:N0}/{c.Allocated:N0} ({c.UsagePercent}%)");

        var summary = $"Budget containing {date} ({result.StartDate} → {result.EndDate}):\n" +
            $"Total: {result.Summary.TotalBudget:N0} | Spent: {result.Summary.TotalSpent:N0} | " +
            $"Remaining: {result.Summary.Remaining:N0} | Reserved: {result.Summary.ReservedRemaining:N0} | " +
            $"Spendable: {result.Summary.SpendableRemaining:N0} | Daily: {result.Summary.DailyBudget:N0} " +
            $"({result.Summary.UsagePercent}% used)\n" +
            string.Join("\n", lines);

        return (summary, result);
    }

    public async Task<(string, object?)> CreateBudgetAsync(Guid userId, JsonElement args)
    {
        var year = TryInt(args, "year", DateTime.UtcNow.Year);
        var month = TryInt(args, "month", DateTime.UtcNow.Month);
        var totalAmount = TryDecimal(args, "total_amount");
        if (totalAmount <= 0)
            return ("Please provide the total budget amount.", null);

        var startDate = TryDateOnly(args, "start_date");
        var endDate = TryDateOnly(args, "end_date");
        if (startDate.HasValue != endDate.HasValue)
            return ("For a custom range, pass both start_date and end_date (yyyy-MM-dd). For a full month, omit both and use year/month.", null);

        CreateBudgetRequest request;
        string summaryLabel;
        if (startDate.HasValue && endDate.HasValue)
        {
            request = new CreateBudgetRequest(
                startDate.Value.Year, startDate.Value.Month, totalAmount, null, startDate, endDate);
            summaryLabel = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}";
        }
        else
        {
            request = new CreateBudgetRequest(year, month, totalAmount);
            summaryLabel = $"{year}-{month:D2}";
        }

        var result = await _budgetService.CreateBudgetAsync(userId, request);

        return ($"Created budget for {summaryLabel}: {totalAmount:N0} total", result);
    }

    public async Task<(string, object?)> UpdateBudgetAsync(Guid userId, JsonElement args)
    {
        var budgetId = await ResolveBudgetIdAsync(userId, args);
        if (string.IsNullOrWhiteSpace(budgetId))
            return ("No budget found. Create one first or specify year/month.", null);

        var totalAmount = TryDecimal(args, "total_amount");
        if (totalAmount <= 0)
            return ("Please provide the new total budget amount.", null);

        var request = new UpdateBudgetRequest(totalAmount);
        var result = await _budgetService.UpdateBudgetAsync(userId, budgetId, request);

        return result is not null
            ? ($"Updated budget total to {totalAmount:N0}", result)
            : ("Budget not found.", null);
    }

    public async Task<(string, object?)> DeleteBudgetAsync(Guid userId, JsonElement args)
    {
        var budgetId = await ResolveBudgetIdAsync(userId, args);
        if (string.IsNullOrWhiteSpace(budgetId))
            return ("No budget found for the specified month.", null);

        var result = await _budgetService.DeleteBudgetAsync(userId, budgetId);
        return result
            ? ("Budget deleted successfully.", true)
            : ("Budget not found.", false);
    }

    public async Task<(string, object?)> AddBudgetCategoryAsync(Guid userId, JsonElement args)
    {
        var budgetId = await ResolveBudgetIdAsync(userId, args);
        if (string.IsNullOrWhiteSpace(budgetId))
        {
            var y = TryInt(args, "year", DateTime.UtcNow.Year);
            var m = TryInt(args, "month", DateTime.UtcNow.Month);
            return ($"No budget found for {y}-{m:D2}. Please create a budget first.", null);
        }

        var categoryId = await ResolveCategoryIdAsync(userId, args);
        if (string.IsNullOrWhiteSpace(categoryId))
            return ("Category not found. Try 'list categories' to see available categories.", null);

        var allocated = TryDecimal(args, "allocated_amount");
        if (allocated <= 0)
            return ("Please provide the allocation amount.", null);

        var threshold = args.TryGetProperty("alert_threshold", out var th) ? th.GetDecimal() : 0.8m;
        var alertsEnabled = TryBool(args, "alerts_enabled", true);

        var request = new CreateBudgetCategoryRequest(categoryId, allocated, threshold, AlertsEnabled: alertsEnabled);
        var result = await _budgetService.AddCategoryAsync(userId, budgetId, request);

        return result is not null
            ? ($"Added category to budget: {result.Name} — {allocated:N0} allocated", result)
            : ("Failed to add category to budget.", null);
    }

    public async Task<(string, object?)> RemoveBudgetCategoryAsync(Guid userId, JsonElement args)
    {
        var budgetCategoryId = TryStr(args, "budget_category_id");

        if (string.IsNullOrWhiteSpace(budgetCategoryId))
        {
            var categoryName = TryStr(args, "category") ?? TryStr(args, "name");
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var year = TryInt(args, "year", DateTime.UtcNow.Year);
                var month = TryInt(args, "month", DateTime.UtcNow.Month);
                var budget = await _budgetService.GetByMonthAsync(userId, year, month);
                var match = budget?.Categories.FirstOrDefault(c =>
                    c.Name.Contains(categoryName, StringComparison.OrdinalIgnoreCase));
                budgetCategoryId = match?.BudgetCategoryId;
            }
        }

        if (string.IsNullOrWhiteSpace(budgetCategoryId))
            return ("Budget category not found. Try 'show budget' to see allocations.", null);

        var result = await _budgetService.RemoveCategoryAsync(userId, budgetCategoryId);
        return result
            ? ("Removed category from budget.", true)
            : ("Budget category not found.", false);
    }

    private async Task<string?> ResolveBudgetIdAsync(Guid userId, JsonElement args)
    {
        var budgetId = TryStr(args, "budget_id");
        if (!string.IsNullOrWhiteSpace(budgetId)) return budgetId;

        var year = TryInt(args, "year", DateTime.UtcNow.Year);
        var month = TryInt(args, "month", DateTime.UtcNow.Month);
        var budget = await _budgetService.GetByMonthAsync(userId, year, month);
        return budget?.BudgetId;
    }

    private async Task<string?> ResolveCategoryIdAsync(Guid userId, JsonElement args)
    {
        var categoryId = TryStr(args, "category_id");
        if (!string.IsNullOrWhiteSpace(categoryId)) return categoryId;

        var categoryName = TryStr(args, "category") ?? TryStr(args, "name");
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
            {
                Type = Domain.Shared.Constants.AppConstants.TransactionType.Expense,
                Keyword = categoryName,
                PageSize = 1
            });
            if (categories.Items.Count > 0)
                return categories.Items[0].CategoryId.ToString();
        }

        return null;
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static decimal TryDecimal(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0;

    private static int TryInt(JsonElement args, string prop, int fallback) =>
        args.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : fallback;

    private static bool TryBool(JsonElement args, string prop, bool fallback) =>
        args.TryGetProperty(prop, out var v) && (v.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? v.GetBoolean()
            : fallback;

    private static DateOnly? TryDateOnly(JsonElement args, string prop)
    {
        if (!args.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        var s = v.GetString();
        return DateOnly.TryParse(s, out var d) ? d : null;
    }
}
