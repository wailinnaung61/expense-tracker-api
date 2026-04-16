using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IAggregationRepository
{
    Task UpdateRedisCacheAsync(Transaction transaction, string? previousDate = null);
    Task<Aggregation?> GetDailyAggregationAsync(Guid userId, string date);
    Task<List<Aggregation>> GetDailyAggregationsRangeAsync(Guid userId, string startDate, string endDate);
    Task<Aggregation?> GetWeeklyAggregationAsync(Guid userId, string week);
    Task<List<Aggregation>> GetWeeklyAggregationsRangeAsync(Guid userId, string startWeek, string endWeek);
    Task<Aggregation?> GetMonthlyAggregationAsync(Guid userId, string month);
    Task<List<Aggregation>> GetMonthlyAggregationsRangeAsync(Guid userId, string startMonth, string endMonth);
    Task<Aggregation> GetCustomDateSummaryAsync(Guid userId, string startDate, string endDate);
    Task<Aggregation?> GetYearlyAggregationAsync(Guid userId, string year);
    Task<List<Aggregation>> GetYearlyAggregationsRangeAsync(Guid userId, string startYear, string endYear);
    Task<List<CategoryAggregation>> GetCategoryMonthlyAggregationsAsync(Guid userId, string month);
    Task<CategoryAggregation?> GetCategoryMonthlyAggregationAsync(Guid userId, Guid categoryId, string month);
    Task<List<CategoryAggregation>> GetCategoryAggregationsByDateRangeAsync(Guid userId, string startDate, string endDate);
}
