using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatContextLoader
{
    private readonly IMemberRepository _memberRepository;
    private readonly IExpenseCategoryService _categoryService;
    private readonly INotificationService _notificationService;
    private readonly IBudgetService _budgetService;
    private readonly ISavingGoalService _savingGoalService;
    private readonly ITranactionService _transactionService;
    private readonly IDistributedCache _cache;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    public ChatContextLoader(
        IMemberRepository memberRepository,
        IExpenseCategoryService categoryService,
        INotificationService notificationService,
        IBudgetService budgetService,
        ISavingGoalService savingGoalService,
        ITranactionService transactionService,
        IDistributedCache cache)
    {
        _memberRepository = memberRepository;
        _categoryService = categoryService;
        _notificationService = notificationService;
        _budgetService = budgetService;
        _savingGoalService = savingGoalService;
        _transactionService = transactionService;
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
        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());

        var now = DateTime.UtcNow;
        // IMPORTANT: execute sequentially to avoid concurrent EF operations
        // on the same scoped DbContext instance.
        var categoriesResult = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest { PageSize = 100 });
        var notificationsResult = await _notificationService.GetNotificationsAsync(userId, false, 5, null);
        var budgetResult = await _budgetService.GetByMonthAsync(userId, now.Year, now.Month);
        var savingsResult = await _savingGoalService.GetDashboardAsync(userId);
        var recentTxResult = await _transactionService.GetTransactionsAsync(userId, new TransactionFilterRequest { PageSize = 5 });

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
            recentTransactions
        );
    }
}
