using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IAggregationService
{
    Task<DailyAggregation?> GetDailyAggregationAsync(Guid userId, string date);
    Task<List<DailyAggregation>> GetDailyAggregationsRangeAsync(Guid userId, string startDate, string endDate);
    Task<WeeklyAggregation?> GetWeeklyAggregationAsync(Guid userId, string week);
    Task<List<WeeklyAggregation>> GetWeeklyAggregationsRangeAsync(Guid userId, string startWeek, string endWeek);
    Task<MonthlyAggregation?> GetMonthlyAggregationAsync(Guid userId, string month);
    Task<List<MonthlyAggregation>> GetMonthlyAggregationsRangeAsync(Guid userId, string startMonth, string endMonth);
    Task<YearlyAggregation?> GetYearlyAggregationAsync(Guid userId, string year);
    Task<List<YearlyAggregation>> GetYearlyAggregationsRangeAsync(Guid userId, string startYear, string endYear);
    Task<List<CategoryMonthlyAggregation>> GetCategoryMonthlyAggregationsAsync(Guid userId, string month);
    Task<ExpenseBreakdown> GetExpenseBreakdownAsync(Guid userId, string month);
    Task<ExpenseBreakdown> GetExpenseBreakdownByRangeAsync(Guid userId, string startDate, string endDate);
}
