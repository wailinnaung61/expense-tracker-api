using System.Text.Json;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class SavingGoalRepository : ISavingGoalRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SavingGoalRepository> _logger;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public SavingGoalRepository(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<SavingGoalRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SavingGoal?> GetByIdAsync(Guid userId, Guid savingGoalId)
    {
        return await _context.SavingGoals
            .AsNoTracking()
            .Include(s => s.Contributions.OrderByDescending(c => c.ContributionDate))
            .FirstOrDefaultAsync(s => s.UserId == userId.ToString()
                                   && s.SavingGoalId == savingGoalId.ToString());
    }

    public async Task<(List<SavingGoal> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId,
        AppConstants.SavingGoalStatus? status,
        AppConstants.SavingGoalType? goalType,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        Guid? cursorId)
    {
        var query = _context.SavingGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId.ToString());

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (goalType.HasValue)
            query = query.Where(s => s.SavingGoalType == goalType.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(s => EF.Functions.ILike(s.GoalName, $"%{keyword}%")
                                  || EF.Functions.ILike(s.Description, $"%{keyword}%"));

        var totalCount = await query.CountAsync();

        if (cursor.HasValue && cursorId.HasValue)
        {
            var cId = cursorId.Value.ToString();
            query = query.Where(s => s.CreatedAt < cursor.Value
                || (s.CreatedAt == cursor.Value && string.Compare(s.SavingGoalId, cId) < 0));
        }

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.SavingGoalId)
            .Take(pageSize + 1)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<SavingGoal>> GetAllForDashboardAsync(Guid userId)
    {
        var cacheKey = $"saving:dashboard:raw:{userId}";
        try
        {
            var bytes = await _cache.GetAsync(cacheKey);
            if (bytes is not null)
                return JsonSerializer.Deserialize<List<SavingGoal>>(bytes)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for key {Key}", cacheKey);
        }

        var items = await _context.SavingGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId.ToString())
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(items);
            await _cache.SetAsync(cacheKey, bytes, CacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for key {Key}", cacheKey);
        }

        return items;
    }

    public async Task<List<SavingGoal>> GetAllForDashboardByRangeAsync(Guid userId, string startDate, string endDate)
    {
        var cacheKey = $"saving:dashboard:range:{userId}:{startDate}:{endDate}";
        try
        {
            var bytes = await _cache.GetAsync(cacheKey);
            if (bytes is not null)
                return JsonSerializer.Deserialize<List<SavingGoal>>(bytes)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for key {Key}", cacheKey);
        }

        var start = DateTime.SpecifyKind(DateOnly.Parse(startDate).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(DateOnly.Parse(endDate).ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var items = await _context.SavingGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId.ToString() && s.CreatedAt >= start && s.CreatedAt <= end)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(items);
            await _cache.SetAsync(cacheKey, bytes, CacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for key {Key}", cacheKey);
        }

        return items;
    }

    public async Task<SavingGoal> CreateAsync(SavingGoal savingGoal)
    {
        savingGoal.CreatedAt = DateTime.UtcNow;
        await _context.SavingGoals.AddAsync(savingGoal);
        await _context.SaveChangesAsync();
        return savingGoal;
    }

    public async Task<SavingGoal?> UpdateAsync(SavingGoal savingGoal)
    {
        var existing = await _context.SavingGoals
            .FirstOrDefaultAsync(s => s.UserId == savingGoal.UserId
                                   && s.SavingGoalId == savingGoal.SavingGoalId);
        if (existing is null) return null;

        existing.GoalName = savingGoal.GoalName;
        existing.Description = savingGoal.Description;
        existing.TargetAmount = savingGoal.TargetAmount;
        existing.CurrentAmount = savingGoal.CurrentAmount;
        existing.TargetDate = savingGoal.TargetDate;
        existing.Status = savingGoal.Status;
        existing.SavingGoalType = savingGoal.SavingGoalType;
        existing.Notes = savingGoal.Notes;
        existing.Icon = savingGoal.Icon;
        existing.Color = savingGoal.Color;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid savingGoalId)
    {
        var goal = await _context.SavingGoals
            .FirstOrDefaultAsync(s => s.UserId == userId.ToString()
                                   && s.SavingGoalId == savingGoalId.ToString());
        if (goal is null) return false;

        _context.SavingGoals.Remove(goal);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task InvalidateCacheAsync(string userId)
    {
        var key = $"saving:dashboard:raw:{userId}";
        try { await _cache.RemoveAsync(key); }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache invalidation failed for {Key}", key); }

        try
        {
            var prefix = $"ExpenseTracker:saving:dashboard:range:{userId}:";
            // No Redis multiplexer injected here; remove known broad wildcard-free fallback not available.
            // Range cache entries are short-lived and will expire automatically.
            _logger.LogDebug("Skipping wildcard invalidation for saving range cache prefix {Prefix}", prefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Range cache invalidation warning for user {UserId}", userId);
        }
    }
}
