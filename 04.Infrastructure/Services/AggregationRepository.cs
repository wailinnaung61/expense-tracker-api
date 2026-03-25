using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class AggregationRepository : IAggregationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AggregationRepository> _logger;

    public AggregationRepository(
        ApplicationDbContext context,
        ILogger<AggregationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpdateAggregationsAsync(Transaction transaction)
    {
        await RefreshMaterializedViewsAsync();
    }

    public async Task ReverseAggregationsAsync(Transaction transaction)
    {
        await RefreshMaterializedViewsAsync();
    }

    // ============================================================================
    // DAILY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetDailyAggregationAsync(Guid userId, string date)
    {
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

        return row is null ? null : MapToAggregation(row);
    }

    public async Task<List<Aggregation>> GetDailyAggregationsRangeAsync(Guid userId, string startDate, string endDate)
    {
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

        return row is null ? null : MapToAggregation(row);
    }

    public async Task<List<Aggregation>> GetWeeklyAggregationsRangeAsync(Guid userId, string startWeek, string endWeek)
    {
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

        return row is null ? null : MapToAggregation(row);
    }

    public async Task<List<Aggregation>> GetMonthlyAggregationsRangeAsync(Guid userId, string startMonth, string endMonth)
    {
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

    // ============================================================================
    // YEARLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetYearlyAggregationAsync(Guid userId, string year)
    {
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

        return row is null ? null : MapToAggregation(row);
    }

    public async Task<List<Aggregation>> GetYearlyAggregationsRangeAsync(Guid userId, string startYear, string endYear)
    {
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

        return rows.Select(MapToCategoryAggregation).ToList();
    }

    public async Task<CategoryAggregation?> GetCategoryMonthlyAggregationAsync(Guid userId, Guid categoryId, string month)
    {
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

        return row is null ? null : MapToCategoryAggregation(row);
    }

    // ============================================================================
    // REFRESH
    // ============================================================================

    private async Task RefreshMaterializedViewsAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_aggregations");
            await _context.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_weekly_aggregations");
            await _context.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_monthly_aggregations");
            await _context.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_yearly_aggregations");
            await _context.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_category_monthly_aggregations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh materialized views");
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
