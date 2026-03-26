using System.Text.Json;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class AggregationRepository : IAggregationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AggregationRepository> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheDuration
    };

    public AggregationRepository(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<AggregationRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    // Called on transaction create/update/delete to invalidate user's cache
    public async Task UpdateRedisCacheAsync(Transaction transaction)
    {
        await InvalidateUserCacheAsync(transaction.UserId);
    }

    // ============================================================================
    // DAILY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetDailyAggregationAsync(Guid userId, string date)
    {
        var cacheKey = $"agg:{userId}:daily:{date}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_daily_aggregations");
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
        var cacheKey = $"agg:{userId}:daily-range:{startDate}:{endDate}";
        var cached = await GetFromCacheAsync<List<Aggregation>>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_daily_aggregations");
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

        var result = rows.Select(MapToAggregation).ToList();
        await SetCacheAsync(cacheKey, result);
        return result;
    }

    // ============================================================================
    // WEEKLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetWeeklyAggregationAsync(Guid userId, string week)
    {
        var cacheKey = $"agg:{userId}:weekly:{week}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_weekly_aggregations");
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
        var cacheKey = $"agg:{userId}:weekly-range:{startWeek}:{endWeek}";
        var cached = await GetFromCacheAsync<List<Aggregation>>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_weekly_aggregations");
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

        var result = rows.Select(MapToAggregation).ToList();
        await SetCacheAsync(cacheKey, result);
        return result;
    }

    // ============================================================================
    // MONTHLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetMonthlyAggregationAsync(Guid userId, string month)
    {
        var cacheKey = $"agg:{userId}:monthly:{month}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_monthly_aggregations");
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
        var cacheKey = $"agg:{userId}:monthly-range:{startMonth}:{endMonth}";
        var cached = await GetFromCacheAsync<List<Aggregation>>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_monthly_aggregations");
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

        var result = rows.Select(MapToAggregation).ToList();
        await SetCacheAsync(cacheKey, result);
        return result;
    }

    // ============================================================================
    // YEARLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetYearlyAggregationAsync(Guid userId, string year)
    {
        var cacheKey = $"agg:{userId}:yearly:{year}";
        var cached = await GetFromCacheAsync<Aggregation>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_yearly_aggregations");
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
        var cacheKey = $"agg:{userId}:yearly-range:{startYear}:{endYear}";
        var cached = await GetFromCacheAsync<List<Aggregation>>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_yearly_aggregations");
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

        var result = rows.Select(MapToAggregation).ToList();
        await SetCacheAsync(cacheKey, result);
        return result;
    }

    // ============================================================================
    // CATEGORY AGGREGATIONS
    // ============================================================================

    public async Task<List<CategoryAggregation>> GetCategoryMonthlyAggregationsAsync(Guid userId, string month)
    {
        var cacheKey = $"agg:{userId}:cat-monthly:{month}";
        var cached = await GetFromCacheAsync<List<CategoryAggregation>>(cacheKey);
        if (cached is not null) return cached;

        await RefreshMaterializedViewAsync("mv_category_monthly_aggregations");
        var userIdStr = userId.ToString();

        var rows = await _context.Database
            .SqlQuery<CategoryAggregationRow>($"""
                SELECT
                    category_id,
                    period,
                    TO_CHAR(period_start, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR(period_end, 'YYYY/MM/DD') AS period_end,
                    total_amount, transaction_count
                FROM mv_category_monthly_aggregations
                WHERE user_id = {userIdStr} AND period = {month}
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

        await RefreshMaterializedViewAsync("mv_category_monthly_aggregations");
        var userIdStr = userId.ToString();
        var categoryIdStr = categoryId.ToString();

        var row = await _context.Database
            .SqlQuery<CategoryAggregationRow>($"""
                SELECT
                    category_id,
                    period,
                    TO_CHAR(period_start, 'YYYY/MM/DD') AS period_start,
                    TO_CHAR(period_end, 'YYYY/MM/DD') AS period_end,
                    total_amount, transaction_count
                FROM mv_category_monthly_aggregations
                WHERE user_id = {userIdStr}
                    AND category_id = {categoryIdStr}
                    AND period = {month}
                """)
            .FirstOrDefaultAsync();

        var result = row is null ? null : MapToCategoryAggregation(row);
        if (result is not null) await SetCacheAsync(cacheKey, result);
        return result;
    }

    // ============================================================================
    // REFRESH (only the view that's needed)
    // ============================================================================

    private async Task RefreshMaterializedViewAsync(string viewName)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync($"REFRESH MATERIALIZED VIEW CONCURRENTLY {viewName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh materialized view {ViewName}", viewName);
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

    private async Task InvalidateUserCacheAsync(string userId)
    {
        // Remove known cache keys for this user's current period
        var now = DateTime.UtcNow;
        var today = now.ToString("yyyy-MM-dd");
        var month = now.ToString("yyyy-MM");
        var year = now.ToString("yyyy");
        var week = $"{System.Globalization.ISOWeek.GetYear(now)}-W{System.Globalization.ISOWeek.GetWeekOfYear(now):D2}";

        var keysToRemove = new[]
        {
            $"agg:{userId}:daily:{today}",
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
        public string period { get; set; } = string.Empty;
        public string period_start { get; set; } = string.Empty;
        public string period_end { get; set; } = string.Empty;
        public decimal total_amount { get; set; }
        public int transaction_count { get; set; }
    }
}
