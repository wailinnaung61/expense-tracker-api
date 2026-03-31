using System.Text.Json;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace _04.Infrastructure.Services;

public class InvestmentRepository : IInvestmentRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IDatabase _redis;
    private readonly ILogger<InvestmentRepository> _logger;

    private static readonly DistributedCacheEntryOptions DashboardCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    private static readonly DistributedCacheEntryOptions AllocationCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
    };

    private static readonly DistributedCacheEntryOptions TopCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public InvestmentRepository(
        ApplicationDbContext context,
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<InvestmentRepository> logger)
    {
        _context = context;
        _cache = cache;
        _redis = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<(List<Investment> Items, int TotalCount)> GetInvestmentsAsync(
        string userId,
        string? portfolioId,
        AppConstants.AssetType? assetType,
        AppConstants.InvestmentStatus? status,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        string? cursorId)
    {
        var query = _context.Investments
            .AsNoTracking()
            .Where(i => i.UserId == userId);

        if (!string.IsNullOrEmpty(portfolioId))
            query = query.Where(i => i.PortfolioId == portfolioId);

        if (assetType.HasValue)
            query = query.Where(i => i.AssetType == assetType.Value);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(i => EF.Functions.ILike(i.AssetName, $"%{keyword}%")
                                  || EF.Functions.ILike(i.Symbol, $"%{keyword}%"));

        var totalCount = await query.CountAsync();

        if (cursor.HasValue && !string.IsNullOrEmpty(cursorId))
        {
            query = query.Where(i =>
                i.CreatedAt < cursor.Value ||
                (i.CreatedAt == cursor.Value && string.Compare(i.InvestmentId, cursorId) < 0));
        }

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.InvestmentId)
            .Take(pageSize + 1)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Investment?> GetByIdAsync(Guid userId, Guid investmentId)
    {
        return await _context.Investments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvestmentId == investmentId.ToString()
                                   && i.UserId == userId.ToString());
    }

    public async Task<Investment> CreateAsync(Investment investment)
    {
        investment.CreatedAt = DateTime.UtcNow;
        await _context.Investments.AddAsync(investment);
        await _context.SaveChangesAsync();
        return investment;
    }

    public async Task<Investment?> UpdateAsync(Investment investment)
    {
        var existing = await _context.Investments
            .FirstOrDefaultAsync(i => i.InvestmentId == investment.InvestmentId
                                   && i.UserId == investment.UserId);

        if (existing is null) return null;

        existing.PortfolioId = investment.PortfolioId;
        existing.AssetName = investment.AssetName;
        existing.Symbol = investment.Symbol;
        existing.Quantity = investment.Quantity;
        existing.PurchasePrice = investment.PurchasePrice;
        existing.CurrentPrice = investment.CurrentPrice;
        existing.PurchaseDate = investment.PurchaseDate;
        existing.Status = investment.Status;
        existing.Notes = investment.Notes;
        existing.ImageUrl = investment.ImageUrl;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid investmentId)
    {
        var investment = await _context.Investments
            .FirstOrDefaultAsync(i => i.InvestmentId == investmentId.ToString()
                                   && i.UserId == userId.ToString());

        if (investment is null) return false;

        _context.Investments.Remove(investment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Investment>> GetAllForDashboardAsync(string userId)
    {
        var cacheKey = $"investment:dashboard:raw:{userId}";

        try
        {
            var bytes = await _cache.GetAsync(cacheKey);
            if (bytes is not null)
                return JsonSerializer.Deserialize<List<Investment>>(bytes)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}", cacheKey);
        }

        var items = await _context.Investments
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .ToListAsync();

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(items);
            await _cache.SetAsync(cacheKey, bytes, DashboardCacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {Key}", cacheKey);
        }

        return items;
    }

    // ============================================================================
    // CACHE
    // ============================================================================

    public async Task<T?> GetFromCacheAsync<T>(string key) where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key);
            if (bytes is null) return null;
            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetInCacheAsync<T>(string key, T value, DistributedCacheEntryOptions options)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {Key}", key);
        }
    }

    public DistributedCacheEntryOptions GetDashboardOptions() => DashboardCacheOptions;
    public DistributedCacheEntryOptions GetAllocationOptions() => AllocationCacheOptions;
    public DistributedCacheEntryOptions GetTopOptions() => TopCacheOptions;

    public async Task InvalidateCacheAsync(string userId)
    {
        var fixedKeys = new[]
        {
            $"investment:dashboard:{userId}",
            $"investment:dashboard:raw:{userId}",
            $"investment:allocation:{userId}",
            $"investment:top:{userId}",
            $"investment:portfolio:list:{userId}"
        };

        foreach (var key in fixedKeys)
        {
            try { await _cache.RemoveAsync(key); }
            catch (Exception ex) { _logger.LogWarning(ex, "Cache invalidation failed for key {Key}", key); }
        }

        // Wildcard-remove investment:list:{userId}:* via SCAN
        try
        {
            var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());
            var pattern = $"ExpenseTracker:investment:list:{userId}:*";

            foreach (var redisKey in server.Keys(pattern: pattern))
                await _cache.RemoveAsync(redisKey.ToString().Replace("ExpenseTracker:", string.Empty));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wildcard cache invalidation failed for user {UserId}", userId);
        }

        _logger.LogInformation("Investment cache invalidated for user {UserId}", userId);
    }
}
