using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;

namespace expense_tracker_backend.Application.Services;

public class AggregationService : IAggregationService
{
    private readonly IAggregationRepository _repository;
    private readonly IExpenseCategoryRepository _categoryRepository;

    public AggregationService(
        IAggregationRepository repository,
        IExpenseCategoryRepository categoryRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
    }

    public async Task<DailyAggregation?> GetDailyAggregationAsync(Guid userId, string date)
    {
        var doc = await _repository.GetDailyAggregationAsync(userId, date);
        return doc == null ? null : MapToDailyAggregation(doc);
    }

    public async Task<List<DailyAggregation>> GetDailyAggregationsRangeAsync(Guid userId, string startDate, string endDate)
    {
        var docs = await _repository.GetDailyAggregationsRangeAsync(userId, startDate, endDate);
        return docs.Select(MapToDailyAggregation).ToList();
    }

    public async Task<WeeklyAggregation?> GetWeeklyAggregationAsync(Guid userId, string week)
    {
        var doc = await _repository.GetWeeklyAggregationAsync(userId, week);
        return doc == null ? null : MapToWeeklyAggregation(doc);
    }

    public async Task<List<WeeklyAggregation>> GetWeeklyAggregationsRangeAsync(Guid userId, string startWeek, string endWeek)
    {
        var docs = await _repository.GetWeeklyAggregationsRangeAsync(userId, startWeek, endWeek);
        return docs.Select(MapToWeeklyAggregation).ToList();
    }

    public async Task<MonthlyAggregation?> GetMonthlyAggregationAsync(Guid userId, string month)
    {
        var doc = await _repository.GetMonthlyAggregationAsync(userId, month);
        return doc == null ? null : MapToMonthlyAggregation(doc);
    }

    public async Task<List<MonthlyAggregation>> GetMonthlyAggregationsRangeAsync(Guid userId, string startMonth, string endMonth)
    {
        var docs = await _repository.GetMonthlyAggregationsRangeAsync(userId, startMonth, endMonth);
        return docs.Select(MapToMonthlyAggregation).ToList();
    }

    public async Task<YearlyAggregation?> GetYearlyAggregationAsync(Guid userId, string year)
    {
        var doc = await _repository.GetYearlyAggregationAsync(userId, year);
        return doc == null ? null : MapToYearlyAggregation(doc);
    }

    public async Task<List<YearlyAggregation>> GetYearlyAggregationsRangeAsync(Guid userId, string startYear, string endYear)
    {
        var docs = await _repository.GetYearlyAggregationsRangeAsync(userId, startYear, endYear);
        return docs.Select(MapToYearlyAggregation).ToList();
    }

    public async Task<List<CategoryMonthlyAggregation>> GetCategoryMonthlyAggregationsAsync(Guid userId, string month)
    {
        var docs = await _repository.GetCategoryMonthlyAggregationsAsync(userId, month);
        return docs.Select(MapToCategoryMonthlyAggregation).ToList();
    }

    public async Task<ExpenseBreakdown> GetExpenseBreakdownAsync(Guid userId, string month)
    {
        // Get category aggregations for the current month
        var categoryDocs = await _repository.GetCategoryMonthlyAggregationsAsync(userId, month);
        
        // Calculate total expenses
        var totalExpenses = categoryDocs.Sum(x => x.TotalAmount);

        // Get category details and build breakdown items
        var breakdownItems = new List<CategoryBreakdownItem>();
        
        foreach (var doc in categoryDocs)
        {
            var category = await _categoryRepository.GetExpenseCategoryByIdAsync(userId, Guid.Parse(doc.CategoryId));
            var percentage = totalExpenses > 0 ? (double)(doc.TotalAmount / totalExpenses * 100) : 0;
            
            breakdownItems.Add(new CategoryBreakdownItem(
                doc.CategoryId,
                category?.DisplayName ?? "Unknown",
                doc.TotalAmount,
                Math.Round(percentage, 1)
            ));
        }

        // Get comparison with last month
        MonthlyComparison? comparison = null;
        var currentMonthAgg = await _repository.GetMonthlyAggregationAsync(userId, month);
        
        if (currentMonthAgg != null)
        {
            // Parse month (format: 2026-03) and get previous month
            if (DateTime.TryParse($"{month}-01", out var currentDate))
            {
                var lastMonthDate = currentDate.AddMonths(-1);
                var lastMonth = lastMonthDate.ToString("yyyy-MM");
                var lastMonthAgg = await _repository.GetMonthlyAggregationAsync(userId, lastMonth);

                if (lastMonthAgg != null)
                {
                    var thisMonth = currentMonthAgg.Expense;
                    var lastMonthExpense = lastMonthAgg.Expense;
                    var difference = thisMonth - lastMonthExpense;
                    var percentageChange = lastMonthExpense > 0 
                        ? (double)(difference / lastMonthExpense * 100) 
                        : 0;

                    comparison = new MonthlyComparison(
                        lastMonthExpense,
                        thisMonth,
                        difference,
                        Math.Round(percentageChange, 1)
                    );
                }
            }
        }

        return new ExpenseBreakdown(
            totalExpenses,
            breakdownItems.OrderByDescending(x => x.Amount).ToList(),
            comparison
        );
    }

    public async Task<ExpenseBreakdown> GetExpenseBreakdownByRangeAsync(Guid userId, string startDate, string endDate)
    {
        var categoryDocs = await _repository.GetCategoryAggregationsByDateRangeAsync(userId, startDate, endDate);

        var totalExpenses = categoryDocs.Sum(x => x.TotalAmount);

        var breakdownItems = new List<CategoryBreakdownItem>();
        foreach (var doc in categoryDocs)
        {
            var category = await _categoryRepository.GetExpenseCategoryByIdAsync(userId, Guid.Parse(doc.CategoryId));
            var percentage = totalExpenses > 0 ? (double)(doc.TotalAmount / totalExpenses * 100) : 0;

            breakdownItems.Add(new CategoryBreakdownItem(
                doc.CategoryId,
                category?.DisplayName ?? "Unknown",
                doc.TotalAmount,
                Math.Round(percentage, 1)
            ));
        }

        MonthlyComparison? comparison = null;
        if (DateOnly.TryParse(startDate, out var start) && DateOnly.TryParse(endDate, out var end))
        {
            var days = end.DayNumber - start.DayNumber + 1;
            var prevEnd = start.AddDays(-1);
            var prevStart = prevEnd.AddDays(-(days - 1));

            var prevDocs = await _repository.GetCategoryAggregationsByDateRangeAsync(
                userId, prevStart.ToString("yyyy-MM-dd"), prevEnd.ToString("yyyy-MM-dd"));

            var prevTotal = prevDocs.Sum(x => x.TotalAmount);
            if (prevTotal > 0 || totalExpenses > 0)
            {
                var difference = totalExpenses - prevTotal;
                var percentageChange = prevTotal > 0
                    ? (double)(difference / prevTotal * 100)
                    : 0;

                comparison = new MonthlyComparison(
                    prevTotal,
                    totalExpenses,
                    difference,
                    Math.Round(percentageChange, 1)
                );
            }
        }

        return new ExpenseBreakdown(
            totalExpenses,
            breakdownItems.OrderByDescending(x => x.Amount).ToList(),
            comparison
        );
    }

    private static DailyAggregation MapToDailyAggregation(Aggregation doc) => new(
        doc.Period,
        doc.Income,
        doc.Expense,
        doc.Saving,
        doc.Investment,
        doc.TransactionCount
    );

    private static WeeklyAggregation MapToWeeklyAggregation(Aggregation doc) => new(
        doc.Period,
        doc.Income,
        doc.Expense,
        doc.Saving,
        doc.Investment,
        doc.TransactionCount
    );

    private static MonthlyAggregation MapToMonthlyAggregation(Aggregation doc) => new(
        doc.Period,
        doc.PeriodStart ?? string.Empty,
        doc.PeriodEnd ?? string.Empty,
        doc.Income,
        doc.Expense,
        doc.Saving,
        doc.Investment,
        doc.TransactionCount
    );

    private static YearlyAggregation MapToYearlyAggregation(Aggregation doc) => new(
        doc.Period,
        doc.Income,
        doc.Expense,
        doc.Saving,
        doc.Investment,
        doc.TransactionCount
    );

    private static CategoryMonthlyAggregation MapToCategoryMonthlyAggregation(CategoryAggregation doc) => new(
        doc.CategoryId,
        doc.Period,
        doc.PeriodStart ?? string.Empty,
        doc.PeriodEnd ?? string.Empty,
        doc.TotalAmount,
        doc.TransactionCount
    );
}
