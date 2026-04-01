using System.Text.Json;
using System.Text.Json.Serialization;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class BudgetRepository : IBudgetRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<BudgetRepository> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheDuration
    };

    // Handles circular references between Budget ↔ BudgetCategory navigation properties
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public BudgetRepository(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<BudgetRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Budget?> GetByIdAsync(string userId, string budgetId)
    {
        return await _context.Budgets
            .AsNoTracking()
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Snapshot)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
            .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);
    }

    public async Task<Budget?> GetByMonthAsync(string userId, int year, int month)
    {
        var startDate = new DateOnly(year, month, 1).ToString("yyyy-MM-dd");

        var cacheKey = BuildCacheKey(userId, year, month);
        var cached = await GetFromCacheAsync<Budget>(cacheKey);
        if (cached is not null) return cached;

        var budget = await _context.Budgets
            .AsNoTracking()
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Snapshot)
            .Include(b => b.BudgetCategories)
                .ThenInclude(bc => bc.Category)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.StartDate == startDate);

        if (budget is not null)
            await SetCacheAsync(cacheKey, budget);

        return budget;
    }

    public async Task<BudgetCategory?> GetBudgetCategoryAsync(string userId, string budgetCategoryId)
    {
        return await _context.BudgetCategories
            .AsNoTracking()
            .Include(bc => bc.Budget)
            .Include(bc => bc.Snapshot)
            .Include(bc => bc.Category)
            .FirstOrDefaultAsync(bc =>
                bc.BudgetCategoryId == budgetCategoryId &&
                bc.Budget!.UserId == userId);
    }

    public async Task<Budget> CreateAsync(Budget budget)
    {
        await _context.Budgets.AddAsync(budget);
        await _context.SaveChangesAsync();

        var startDate = DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
        await InvalidateCacheAsync(budget.UserId, startDate.Year, startDate.Month);

        return budget;
    }

    public async Task<Budget> UpdateAsync(Budget budget)
    {
        var existing = await _context.Budgets
            .FirstOrDefaultAsync(b => b.BudgetId == budget.BudgetId && b.UserId == budget.UserId);

        if (existing is null) return budget;

        existing.TotalAmount = budget.TotalAmount;
        existing.Status = budget.Status;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(string userId, string budgetId)
    {
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.BudgetId == budgetId && b.UserId == userId);

        if (budget is null) return false;

        _context.Budgets.Remove(budget);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<BudgetCategory> AddCategoryAsync(BudgetCategory budgetCategory)
    {
        await _context.BudgetCategories.AddAsync(budgetCategory);
        await _context.SaveChangesAsync();

        // Invalidate cache for this budget's month
        var budget = await _context.Budgets
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BudgetId == budgetCategory.BudgetId);

        if (budget is not null)
        {
            var startDate = DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
            await InvalidateCacheAsync(budget.UserId, startDate.Year, startDate.Month);
        }

        return budgetCategory;
    }

    public async Task<BudgetCategory> UpdateBudgetCategoryAsync(BudgetCategory budgetCategory)
    {
        var existing = await _context.BudgetCategories
            .FirstOrDefaultAsync(bc => bc.BudgetCategoryId == budgetCategory.BudgetCategoryId);

        if (existing is null) return budgetCategory;

        existing.AllocatedAmount = budgetCategory.AllocatedAmount;
        existing.AlertThreshold = budgetCategory.AlertThreshold;
        existing.SortOrder = budgetCategory.SortOrder;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> RemoveCategoryAsync(string userId, string budgetCategoryId)
    {
        var budgetCategory = await _context.BudgetCategories
            .Include(bc => bc.Budget)
            .FirstOrDefaultAsync(bc =>
                bc.BudgetCategoryId == budgetCategoryId &&
                bc.Budget!.UserId == userId);

        if (budgetCategory is null) return false;

        var startDate = DateOnly.ParseExact(budgetCategory.Budget!.StartDate, "yyyy-MM-dd");

        _context.BudgetCategories.Remove(budgetCategory);
        await _context.SaveChangesAsync();

        await InvalidateCacheAsync(userId, startDate.Year, startDate.Month);
        return true;
    }

    // ── Snapshot update — called on every transaction create/delete ─────────────
    public async Task UpdateSnapshotOnTransactionAsync(
        string userId, string categoryId, string transactionDate, decimal amountDelta, int countDelta)
    {
        // Find the active budget that covers the transaction date
        var snapshot = await _context.BudgetSnapshots
            .Include(s => s.BudgetCategory)
                .ThenInclude(bc => bc!.Budget)
            .FirstOrDefaultAsync(s =>
                s.BudgetCategory!.CategoryId == categoryId &&
                s.BudgetCategory.Budget!.UserId == userId &&
                s.BudgetCategory.Budget.StartDate.CompareTo(transactionDate) <= 0 &&
                s.BudgetCategory.Budget.EndDate.CompareTo(transactionDate) >= 0);

        if (snapshot is null) return;

        snapshot.SpentAmount = Math.Max(0, snapshot.SpentAmount + amountDelta);
        snapshot.TransactionCount = Math.Max(0, snapshot.TransactionCount + countDelta);

        if (amountDelta > 0)
            snapshot.LastTransactionDate = transactionDate;

        snapshot.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var budget = snapshot.BudgetCategory!.Budget!;
        var startDate = DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
        await InvalidateCacheAsync(userId, startDate.Year, startDate.Month);

        _logger.LogInformation(
            "Budget snapshot updated: userId={UserId} categoryId={CategoryId} delta={Delta}",
            userId, categoryId, amountDelta);
    }

    public async Task ResetSnapshotsAsync(string userId, string budgetId)
    {
        var snapshots = await _context.BudgetSnapshots
            .Include(s => s.BudgetCategory)
            .Where(s => s.BudgetCategory!.BudgetId == budgetId &&
                        s.BudgetCategory.Budget!.UserId == userId)
            .ToListAsync();

        foreach (var snapshot in snapshots)
        {
            snapshot.SpentAmount = 0;
            snapshot.TransactionCount = 0;
            snapshot.LastTransactionDate = null;
            snapshot.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task InvalidateCacheAsync(string userId, int year, int month)
    {
        var key = BuildCacheKey(userId, year, month);
        await _cache.RemoveAsync(key);
    }

    // ── Cache helpers ────────────────────────────────────────────────────────────

    private static string BuildCacheKey(string userId, int year, int month) =>
        $"budget:{userId}:{year}:{month:D2}";

    private async Task<T?> GetFromCacheAsync<T>(string key) where T : class
    {
        try
        {
            var json = await _cache.GetStringAsync(key);
            return json is null ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {Key}", key);
            return null;
        }
    }

    private async Task SetCacheAsync<T>(string key, T value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _cache.SetStringAsync(key, json, CacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key {Key}", key);
        }
    }
}
