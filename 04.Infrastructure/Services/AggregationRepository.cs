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

public class AggregationRepository : IAggregationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IDatabase _redis;
    private readonly ILogger<AggregationRepository> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheDuration
    };

    // Lock settings
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(200);

    public AggregationRepository(
        ApplicationDbContext context,
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<AggregationRepository> logger)
    {
        _context = context;
        _cache = cache;
        _redis = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    // Called on transaction create/update/delete to invalidate user's cache
    public async Task UpdateRedisCacheAsync(Transaction transaction, string? previousDate = null)
    {
        await InvalidateUserCacheAsync(transaction.UserId, transaction.TransactionDate);
        if (previousDate is not null && previousDate != transaction.TransactionDate)
            await InvalidateUserCacheAsync(transaction.UserId, previousDate);
    }

    // ============================================================================
    // DAILY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetDailyAggregationAsync(Guid userId, string date)
    {
        var cacheKey = $"agg:{userId}:daily:{date}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshWithLockAsync("mv_daily_aggregations");
        var userIdStr = userId.ToString();
        var dateParam = DateOnly.Parse(date);

        var row = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period::text AS period,
                    '' AS period_start,
                    '' AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_daily_aggregations
                WHERE user_id = {userIdStr} AND period = {dateParam}
                """)
            .FirstOrDefaultAsync();

        var result = row is null ? null : MapToAggregation(row);
        if (result is not null) await SetCacheAsync(cacheKey, result);
        return result;
    }

    public async Task<List<Aggregation>> GetDailyAggregationsRangeAsync(Guid userId, string startDate, string endDate)
    {
        // Not cached: arbitrary [start,end] keys cannot be invalidated reliably on every tx (Redis KEYS / cluster).
        await RefreshWithLockAsync("mv_daily_aggregations");
        var userIdStr = userId.ToString();
        var startParam = DateOnly.Parse(startDate);
        var endParam = DateOnly.Parse(endDate);

        var rows = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period::text AS period,
                    '' AS period_start,
                    '' AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_daily_aggregations
                WHERE user_id = {userIdStr}
                    AND period BETWEEN {startParam} AND {endParam}
                ORDER BY period
                """)
            .ToListAsync();

        return rows.Select(MapToAggregation).ToList();
    }

    // ============================================================================
    // WEEKLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetWeeklyAggregationAsync(Guid userId, string week)
    {
        var cacheKey = $"agg:{userId}:weekly:{week}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshWithLockAsync("mv_weekly_aggregations");
        var userIdStr = userId.ToString();

        var row = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period,
                    '' AS period_start,
                    '' AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_weekly_aggregations
                WHERE user_id = {userIdStr} AND period = {week}
                """)
            .FirstOrDefaultAsync();

        var result = row is null ? null : MapToAggregation(row);
        if (result is not null) await SetCacheAsync(cacheKey, result);
        return result;
    }

    public async Task<List<Aggregation>> GetWeeklyAggregationsRangeAsync(Guid userId, string startWeek, string endWeek)
    {
        await RefreshWithLockAsync("mv_weekly_aggregations");
        var userIdStr = userId.ToString();

        var rows = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period,
                    '' AS period_start,
                    '' AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_weekly_aggregations
                WHERE user_id = {userIdStr}
                    AND period BETWEEN {startWeek} AND {endWeek}
                ORDER BY period
                """)
            .ToListAsync();

        return rows.Select(MapToAggregation).ToList();
    }

    // ============================================================================
    // MONTHLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetMonthlyAggregationAsync(Guid userId, string month)
    {
        var cacheKey = $"agg:{userId}:monthly:{month}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshWithLockAsync("mv_monthly_aggregations");
        var userIdStr = userId.ToString();

        var row = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period,
                    TO_CHAR(period_start, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR(period_end, 'YYYY/MM/DD') AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_monthly_aggregations
                WHERE user_id = {userIdStr} AND period = {month}
                """)
            .FirstOrDefaultAsync();

        var result = row is null ? null : MapToAggregation(row);
        if (result is not null) await SetCacheAsync(cacheKey, result);
        return result;
    }

    public async Task<List<Aggregation>> GetMonthlyAggregationsRangeAsync(Guid userId, string startMonth, string endMonth)
    {
        await RefreshWithLockAsync("mv_monthly_aggregations");
        var userIdStr = userId.ToString();

        var rows = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period,
                    TO_CHAR(period_start, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR(period_end, 'YYYY/MM/DD') AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_monthly_aggregations
                WHERE user_id = {userIdStr}
                    AND period BETWEEN {startMonth} AND {endMonth}
                ORDER BY period
                """)
            .ToListAsync();

        return rows.Select(MapToAggregation).ToList();
    }

    public async Task<Aggregation> GetCustomDateSummaryAsync(Guid userId, string startDate, string endDate)
    {
        await RefreshWithLockAsync("mv_daily_aggregations");
        var userIdStr = userId.ToString();
        var startParam = DateOnly.Parse(startDate);
        var endParam = DateOnly.Parse(endDate);

        var row = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    {startDate + "_to_" + endDate} AS period,
                    TO_CHAR({startParam}, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR({endParam}, 'YYYY/MM/DD') AS period_end,
                    COALESCE(SUM(income), 0) AS income,
                    COALESCE(SUM(expense), 0) AS expense,
                    COALESCE(SUM(saving), 0) AS saving,
                    COALESCE(SUM(investment), 0) AS investment,
                    COALESCE(SUM(transaction_count), 0) AS transaction_count
                FROM mv_daily_aggregations
                WHERE user_id = {userIdStr}
                    AND period BETWEEN {startParam} AND {endParam}
                """)
            .FirstAsync();

        return MapToAggregation(row);
    }

    // ============================================================================
    // YEARLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetYearlyAggregationAsync(Guid userId, string year)
    {
        var cacheKey = $"agg:{userId}:yearly:{year}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshWithLockAsync("mv_yearly_aggregations");
        var userIdStr = userId.ToString();

        var row = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period,
                    '' AS period_start,
                    '' AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_yearly_aggregations
                WHERE user_id = {userIdStr} AND period = {year}
                """)
            .FirstOrDefaultAsync();

        var result = row is null ? null : MapToAggregation(row);
        if (result is not null) await SetCacheAsync(cacheKey, result);
        return result;
    }

    public async Task<List<Aggregation>> GetYearlyAggregationsRangeAsync(Guid userId, string startYear, string endYear)
    {
        await RefreshWithLockAsync("mv_yearly_aggregations");
        var userIdStr = userId.ToString();

        var rows = await _context.Database
            .SqlQuery<AggregationRow>($"""
                SELECT
                    period,
                    '' AS period_start,
                    '' AS period_end,
                    income, expense, saving, investment, transaction_count
                FROM mv_yearly_aggregations
                WHERE user_id = {userIdStr}
                    AND period BETWEEN {startYear} AND {endYear}
                ORDER BY period
                """)
            .ToListAsync();

        return rows.Select(MapToAggregation).ToList();
    }

    // ============================================================================
    // CATEGORY AGGREGATIONS
    // ============================================================================

    public async Task<List<CategoryAggregation>> GetCategoryMonthlyAggregationsAsync(Guid userId, string month)
    {
        var cacheKey = $"agg:{userId}:cat-monthly:{month}";
        var cached = await GetFromCacheAsync<List<CategoryAggregation>>(cacheKey);
        if (cached is not null) return cached;

        await RefreshWithLockAsync("mv_category_monthly_aggregations");
        var userIdStr = userId.ToString();

        var rows = await _context.Database
            .SqlQuery<CategoryAggregationRow>($"""
                SELECT
                    category_id,
                    type,
                    period,
                    TO_CHAR(period_start, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR(period_end, 'YYYY/MM/DD') AS period_end,
                    total_amount, transaction_count
                FROM mv_category_monthly_aggregations
                WHERE user_id = {userIdStr} AND period = {month}
                  AND type = 'EXPENSE'
                """)
            .ToListAsync();

        var result = rows.Select(MapToCategoryAggregation).ToList();
        await SetCacheAsync(cacheKey, result);
        return result;
    }

    public async Task<CategoryAggregation?> GetCategoryMonthlyAggregationAsync(Guid userId, Guid categoryId, string month)
    {
        var cacheKey = $"agg:{userId}:cat-monthly:{categoryId}:{month}";
        var cached = await GetFromCacheAsync<CategoryAggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshWithLockAsync("mv_category_monthly_aggregations");
        var userIdStr = userId.ToString();
        var categoryIdStr = categoryId.ToString();

        var row = await _context.Database
            .SqlQuery<CategoryAggregationRow>($"""
                SELECT
                    category_id,
                    type,
                    period,
                    TO_CHAR(period_start, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR(period_end, 'YYYY/MM/DD') AS period_end,
                    total_amount, transaction_count
                FROM mv_category_monthly_aggregations
                WHERE user_id = {userIdStr}
                    AND category_id = {categoryIdStr}
                    AND period = {month}
                    AND type = 'EXPENSE'
                """)
            .FirstOrDefaultAsync();

        var result = row is null ? null : MapToCategoryAggregation(row);
        if (result is not null) await SetCacheAsync(cacheKey, result);
        return result;
    }

    public async Task<List<CategoryAggregation>> GetCategoryAggregationsByDateRangeAsync(Guid userId, string startDate, string endDate)
    {
        var userIdStr = userId.ToString();
        var result = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userIdStr
                && t.Type == AppConstants.TransactionType.Expense
                && t.Status == AppConstants.PaymentStatus.Completed
                && string.Compare(t.TransactionDate, startDate) >= 0
                && string.Compare(t.TransactionDate, endDate) <= 0
                && !string.IsNullOrEmpty(t.CategoryId))
            .GroupBy(t => t.CategoryId!)
            .Select(g => new CategoryAggregation
            {
                CategoryId = g.Key,
                Period = string.Empty,
                PeriodStart = startDate.Replace('-', '/'),
                PeriodEnd = endDate.Replace('-', '/'),
                TotalAmount = g.Sum(t => t.Amount),
                TransactionCount = g.Count()
            })
            .ToListAsync();

        return result;
    }

    // ============================================================================
    // REFRESH WITH DISTRIBUTED LOCK (only ONE request refreshes at a time)
    // ============================================================================

    private async Task RefreshWithLockAsync(string viewName)
    {
        var lockKey = $"lock:refresh:{viewName}";
        var lockValue = Guid.NewGuid().ToString();

        try
        {
            // Try to acquire lock: SET lockKey lockValue NX EX 30
            var acquired = await _redis.StringSetAsync(lockKey, lockValue, LockExpiry, When.NotExists);

            if (acquired)
            {
                // Winner: refresh the view, then release lock
                try
                {
                    _logger.LogInformation("Lock acquired — refreshing {ViewName}", viewName);
                    await _context.Database.ExecuteSqlRawAsync($"REFRESH MATERIALIZED VIEW CONCURRENTLY {viewName}");
                }
                finally
                {
                    // Release lock only if we still own it (Lua atomic check-and-delete)
                    var script = @"if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
                    await _redis.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockValue });
                }
            }
            else
            {
                // Loser: another request is refreshing — wait for it to finish
                _logger.LogInformation("Lock busy — waiting for {ViewName} refresh to complete", viewName);
                var elapsed = TimeSpan.Zero;
                while (elapsed < LockWaitTimeout)
                {
                    await Task.Delay(LockRetryDelay);
                    elapsed += LockRetryDelay;

                    var stillLocked = await _redis.KeyExistsAsync(lockKey);
                    if (!stillLocked) break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh materialized view {ViewName} with lock", viewName);
        }
    }

    // ============================================================================
    // CACHE HELPERS
    // ============================================================================

    private async Task<T?> GetFromCacheAsync<T>(string key) where T : class
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

    private async Task SetCacheAsync<T>(string key, T value)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, CacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {Key}", key);
        }
    }

    private async Task InvalidateUserCacheAsync(string userId, string transactionDate)
    {
        var txDate = DateOnly.Parse(transactionDate);
        var txDateTime = txDate.ToDateTime(TimeOnly.MinValue);

        var day = txDate.ToString("yyyy-MM-dd");
        var month = txDate.ToString("yyyy-MM");
        var year = txDate.ToString("yyyy");
        var week = $"{System.Globalization.ISOWeek.GetYear(txDateTime)}-W{System.Globalization.ISOWeek.GetWeekOfYear(txDateTime):D2}";

        var keysToRemove = new[]
        {
            $"agg:{userId}:daily:{day}",
            $"agg:{userId}:weekly:{week}",
            $"agg:{userId}:monthly:{month}",
            $"agg:{userId}:yearly:{year}",
            $"agg:{userId}:cat-monthly:{month}",
        };
        foreach (var key in keysToRemove)
        {
            try
            {
                await _cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache invalidation failed for key {Key}", key);
            }
        }

        try
        {
            var patterns = new[]
            {
                $"ExpenseTracker:agg:{userId}:daily-range:*",
                $"ExpenseTracker:agg:{userId}:weekly-range:*",
                $"ExpenseTracker:agg:{userId}:monthly-range:*",
                $"ExpenseTracker:agg:{userId}:yearly-range:*",
                $"ExpenseTracker:agg:{userId}:custom-summary:*",
                $"ExpenseTracker:agg:{userId}:cat-monthly:*:{month}",
                $"ExpenseTracker:agg:{userId}:cat-range:*",
            };

            foreach (var pattern in patterns)
            {
                var keys = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First())
                    .Keys(pattern: pattern, pageSize: 100);

                var redisKeys = keys.ToArray();
                if (redisKeys.Length > 0)
                {
                    await _redis.KeyDeleteAsync(redisKeys);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate range keys for user {UserId}", userId);
        }
    }

    // ============================================================================
    // MAPPERS & ROW TYPES
    // ============================================================================

    private static Aggregation MapToAggregation(AggregationRow row) => new()
    {
        Period = row.period,
        PeriodStart = row.period_start,
        PeriodEnd = row.period_end,
        Income = row.income,
        Expense = row.expense,
        Saving = row.saving,
        Investment = row.investment,
        TransactionCount = row.transaction_count
    };

    private static CategoryAggregation MapToCategoryAggregation(CategoryAggregationRow row) => new()
    {
        CategoryId = row.category_id,
        Period = row.period,
        PeriodStart = row.period_start,
        PeriodEnd = row.period_end,
        TotalAmount = row.total_amount,
        TransactionCount = row.transaction_count
    };

    private class AggregationRow
    {
        public string period { get; set; } = string.Empty;
        public string period_start { get; set; } = string.Empty;
        public string period_end { get; set; } = string.Empty;
        public decimal income { get; set; }
        public decimal expense { get; set; }
        public decimal saving { get; set; }
        public decimal investment { get; set; }
        public int transaction_count { get; set; }
    }

    private class CategoryAggregationRow
    {
        public string category_id { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string period { get; set; } = string.Empty;
        public string period_start { get; set; } = string.Empty;
        public string period_end { get; set; } = string.Empty;
        public decimal total_amount { get; set; }
        public int transaction_count { get; set; }
    }
}
