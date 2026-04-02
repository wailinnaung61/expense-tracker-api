using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace expense_tracker_backend.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DashboardService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<DashboardResponse> GetDashboardAsync(Guid userId, string month)
    {
        var monthDate = DateTime.Parse($"{month}-01");
        var year      = monthDate.Year;
        var monthNum  = monthDate.Month;
        var trendStart       = monthDate.AddMonths(-5).ToString("yyyy-MM");
        var today            = DateTime.UtcNow.Date;
        var todayStr         = today.ToString("yyyy-MM-dd");
        var upcomingEndStr   = today.AddDays(7).ToString("yyyy-MM-dd");

        // Each lambda creates its own DI scope → its own DbContext instance
        // → true parallel execution with no EF Core concurrency crash
        var currentMonthTask     = Scoped<IAggregationService, MonthlyAggregation?>(
            s => s.GetMonthlyAggregationAsync(userId, month));

        var monthlyTrendTask     = Scoped<IAggregationService, List<MonthlyAggregation>>(
            s => s.GetMonthlyAggregationsRangeAsync(userId, trendStart, month));

        var expenseBreakdownTask = Scoped<IAggregationService, ExpenseBreakdown>(
            s => s.GetExpenseBreakdownAsync(userId, month));

        var recentTxTask         = Scoped<ITranactionService, PagedResult<Tranaction>>(
            s => s.GetTransactionsAsync(userId, new TransactionFilterRequest { PageSize = 5 }));

        var budgetTask           = Scoped<IBudgetService, BudgetMonthlyResponse?>(
            s => s.GetByMonthAsync(userId, year, monthNum));

        var savingsTask          = Scoped<ISavingGoalService, SavingDashboardResponse>(
            s => s.GetDashboardAsync(userId));

        var investmentTask       = Scoped<IInvestmentService, InvestmentDashboardResponse>(
            s => s.GetDashboardAsync(userId));

        var upcomingBillsTask    = Scoped<IRecurringPaymentService, List<RecurringPayment>>(
            s => s.GetUpcomingAsync(userId, todayStr, upcomingEndStr));

        await Task.WhenAll(
            currentMonthTask, monthlyTrendTask, expenseBreakdownTask,
            recentTxTask, budgetTask, savingsTask, investmentTask, upcomingBillsTask);

        var upcomingBillDtos = await MapBillsToDtosAsync(userId, upcomingBillsTask.Result);

        return new DashboardResponse(
            currentMonthTask.Result,
            monthlyTrendTask.Result,
            expenseBreakdownTask.Result,
            recentTxTask.Result.Items,
            budgetTask.Result,
            savingsTask.Result,
            investmentTask.Result,
            upcomingBillDtos
        );
    }

    // ── Scope helper ──────────────────────────────────────────────────────────

    private async Task<TResult> Scoped<TService, TResult>(Func<TService, Task<TResult>> operation)
        where TService : notnull
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await operation(service);
    }

    // ── Recurring payment mapper ───────────────────────────────────────────────

    private async Task<List<RecurringPaymentDto>> MapBillsToDtosAsync(Guid userId, List<RecurringPayment> payments)
    {
        if (payments.Count == 0) return [];

        var categoryIds = payments.Select(p => p.CategoryId).Distinct();
        var categories  = new Dictionary<string, string>();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var categorySvc = scope.ServiceProvider.GetRequiredService<IExpenseCategoryService>();

        foreach (var catId in categoryIds)
        {
            var cat = await categorySvc.GetExpenseCategoryByIdAsync(userId, Guid.Parse(catId));
            if (cat is not null) categories[catId] = cat.DisplayName;
        }

        return payments.Select(p =>
        {
            categories.TryGetValue(p.CategoryId, out var name);
            return MapBillToDto(p, name);
        }).ToList();
    }

    private static RecurringPaymentDto MapBillToDto(RecurringPayment p, string? categoryName) => new(
        p.RecurringId,
        Guid.Parse(p.UserId),
        p.Name,
        p.Amount,
        Guid.Parse(p.CategoryId),
        categoryName,
        p.Frequency.ToString().ToUpper(),
        p.NextDueDate.ToString("yyyy-MM-dd"),
        p.LastPaidDate?.ToString("yyyy-MM-dd"),
        p.MissedCount,
        p.Status.ToString().ToUpper(),
        p.CreatedAt,
        p.UpdatedAt ?? p.CreatedAt
    );
}
