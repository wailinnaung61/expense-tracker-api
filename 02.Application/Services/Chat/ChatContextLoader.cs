using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatContextLoader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedCache _cache;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    public ChatContextLoader(IServiceScopeFactory scopeFactory, IDistributedCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    private static string CacheKey(Guid userId) => $"chat:context:{userId}";

    public async Task<ChatContextSnapshot> LoadAsync(Guid userId)
    {
        var cacheKey = CacheKey(userId);
        var cached = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            var deserialized = JsonSerializer.Deserialize<ChatContextSnapshot>(cached);
            if (deserialized is not null) return deserialized;
        }

        var snapshot = await BuildSnapshotAsync(userId);

        var json = JsonSerializer.Serialize(snapshot);
        await _cache.SetStringAsync(cacheKey, json, CacheOptions);

        return snapshot;
    }

    public async Task InvalidateAsync(Guid userId)
    {
        await _cache.RemoveAsync(CacheKey(userId));
    }

    public ChatInitResponse ToInitResponse(ChatContextSnapshot snapshot)
    {
        return new ChatInitResponse(
            snapshot.UserName,
            snapshot.Currency,
            snapshot.Categories,
            snapshot.RecentNotifications,
            snapshot.Budget,
            snapshot.Savings
        );
    }

    private async Task<ChatContextSnapshot> BuildSnapshotAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var month = now.ToString("yyyy-MM");
        var todayStr = now.Date.ToString("yyyy-MM-dd");
        var upcomingEndStr = now.Date.AddDays(14).ToString("yyyy-MM-dd");

        // One DI scope per call → parallel-safe (same pattern as DashboardService)
        var profileTask = Scoped<IMemberRepository, Domain.Entities.MemberProfile?>(
            r => r.GetProfileByUserIdAsync(userId.ToString()));
        var categoriesTask = Scoped<IExpenseCategoryService, PagedResult<ExpenseCategory>>(
            s => s.GetCategoriesAsync(userId, new CategoryFilterRequest { PageSize = 100 }));
        var notificationsTask = Scoped<INotificationService, PagedNotificationResult>(
            s => s.GetNotificationsAsync(userId, false, 5, null));
        var budgetTask = Scoped<IBudgetService, BudgetMonthlyResponse?>(
            s => s.GetByMonthAsync(userId, now.Year, now.Month));
        var savingsTask = Scoped<ISavingGoalService, SavingDashboardResponse>(
            s => s.GetDashboardAsync(userId));
        var recentTxTask = Scoped<ITranactionService, PagedResult<Tranaction>>(
            s => s.GetTransactionsAsync(userId, new TransactionFilterRequest { PageSize = 10 }));
        var monthAggTask = Scoped<IAggregationService, MonthlyAggregation?>(
            s => s.GetMonthlyAggregationAsync(userId, month));
        var breakdownTask = Scoped<IAggregationService, ExpenseBreakdown>(
            s => s.GetExpenseBreakdownAsync(userId, month));
        var billsTask = Scoped<IRecurringPaymentService, List<Domain.Entities.RecurringPayment>>(
            s => s.GetUpcomingAsync(userId, todayStr, upcomingEndStr));
        var investmentTask = Scoped<IInvestmentService, InvestmentDashboardResponse>(
            s => s.GetDashboardAsync(userId));

        await Task.WhenAll(
            profileTask, categoriesTask, notificationsTask, budgetTask, savingsTask,
            recentTxTask, monthAggTask, breakdownTask, billsTask, investmentTask);

        var profile = profileTask.Result;
        var categoriesResult = categoriesTask.Result;
        var notificationsResult = notificationsTask.Result;
        var budgetResult = budgetTask.Result;
        var savingsResult = savingsTask.Result;
        var recentTxResult = recentTxTask.Result;
        var monthAgg = monthAggTask.Result;
        var breakdown = breakdownTask.Result;
        var bills = billsTask.Result;
        var investment = investmentTask.Result;

        var categories = categoriesResult.Items
            .Select(c => new ChatCategoryInfo(c.CategoryId.ToString(), c.DisplayName, c.Type.ToString(), c.Icon))
            .ToList();

        var notifications = notificationsResult.Items
            .Select(n => new ChatNotificationInfo(n.Title, n.Message, n.CreatedAt))
            .ToList();

        ChatBudgetInfo? budget = budgetResult?.Summary is not null
            ? new ChatBudgetInfo(
                budgetResult.Summary.TotalBudget,
                budgetResult.Summary.TotalSpent,
                budgetResult.Summary.Remaining,
                budgetResult.Summary.UsagePercent)
            : null;

        var savings = new ChatSavingsInfo(savingsResult.TotalSaved, savingsResult.ActiveGoalsCount);

        var recentTransactions = recentTxResult.Items
            .Select(tx => new ChatRecentTransaction(tx.type.ToString(), tx.Amount, tx.Description, tx.TranactionDate))
            .ToList();

        ChatMonthTotals? monthTotals = monthAgg is not null
            ? new ChatMonthTotals(monthAgg.Income, monthAgg.Expense, monthAgg.Saving, monthAgg.Investment)
            : null;

        var topCategories = breakdown.Categories
            .OrderByDescending(c => c.Amount)
            .Take(5)
            .Select(c => new ChatCategorySpend(c.CategoryName, c.Amount, (double)c.Percentage))
            .ToList();

        var upcomingBills = bills
            .OrderBy(b => b.NextDueDate)
            .Take(5)
            .Select(b => new ChatUpcomingBill(b.Name, b.Amount, b.NextDueDate.ToString("yyyy-MM-dd")))
            .ToList();

        var investments = new ChatInvestmentTotals(investment.TotalInvested, investment.TotalProfitLoss);

        return new ChatContextSnapshot(
            profile?.UserName,
            profile?.Email,
            profile?.Currency,
            profile?.DailyLimit ?? 0,
            profile?.RoleId,
            profile?.Locale,
            categories,
            notifications,
            budget,
            savings,
            recentTransactions,
            monthTotals,
            topCategories,
            upcomingBills,
            investments);
    }

    private async Task<TResult> Scoped<TService, TResult>(Func<TService, Task<TResult>> operation)
        where TService : notnull
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await operation(service);
    }
}
