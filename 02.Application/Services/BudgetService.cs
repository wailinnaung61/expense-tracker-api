using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.Application.Services;

public class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _repository;
    private readonly IStringLocalizer _localizer;

    public BudgetService(IBudgetRepository repository, IStringLocalizer localizer)
    {
        _repository = repository;
        _localizer = localizer;
    }

    public async Task<BudgetMonthlyResponse?> GetByMonthAsync(Guid userId, int year, int month)
    {
        var budget = await _repository.GetByMonthAsync(userId.ToString(), year, month);
        if (budget is null) return null;

        var totalSpent = budget.BudgetCategories
            .Sum(bc => bc.Snapshot?.SpentAmount ?? 0);

        var startDate = DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
        var endDate = DateOnly.ParseExact(budget.EndDate, "yyyy-MM-dd");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var remainingDays = Math.Max(1, (endDate.DayNumber - today.DayNumber) + 1);
        var remaining = budget.TotalAmount - totalSpent;
        var dailyBudget = Math.Round(remaining / remainingDays, 2);
        var usagePercent = budget.TotalAmount > 0
            ? (int)Math.Round(totalSpent / budget.TotalAmount * 100)
            : 0;

        var categories = budget.BudgetCategories
            .OrderBy(bc => bc.SortOrder)
            .Select(bc => MapToCategoryDto(bc))
            .ToList();

        var topSpending = categories
            .Where(c => budget.TotalAmount > 0)
            .Select(c => new TopSpendingDto(c.Name, (int)Math.Round(c.Spent / budget.TotalAmount * 100)))
            .OrderByDescending(t => t.Percent)
            .Take(5)
            .ToList();

        var summary = new BudgetSummaryDto(
            budget.TotalAmount,
            totalSpent,
            remaining,
            dailyBudget,
            usagePercent);

        return new BudgetMonthlyResponse(
            summary, categories, topSpending, budget.BudgetId, budget.StartDate, budget.EndDate);
    }

    public async Task<BudgetDto> CreateBudgetAsync(Guid userId, CreateBudgetRequest request)
    {
        DateOnly startDate;
        DateOnly endDate;

        if (request.StartDate.HasValue || request.EndDate.HasValue)
        {
            if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                throw new InvalidOperationException(_localizer["BudgetCustomDatesBothRequired"].Value);

            startDate = request.StartDate.Value;
            endDate = request.EndDate.Value;
        }
        else
        {
            startDate = new DateOnly(request.Year, request.Month, 1);
            endDate = startDate.AddMonths(1).AddDays(-1);
        }

        if (endDate < startDate)
            throw new InvalidOperationException(_localizer["BudgetEndBeforeStart"].Value);

        var startIso = startDate.ToString("yyyy-MM-dd");
        var endIso = endDate.ToString("yyyy-MM-dd");

        if (await _repository.HasOverlappingBudgetAsync(userId.ToString(), startIso, endIso))
            throw new InvalidOperationException(_localizer["BudgetDateRangeOverlaps"].Value);

        var periodType = IsFullCalendarMonth(startDate, endDate)
            ? AppConstants.BudgetPeriodType.Monthly
            : AppConstants.BudgetPeriodType.Custom;

        var budget = new Budget
        {
            BudgetId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            PeriodType = periodType,
            StartDate = startIso,
            EndDate = endIso,
            TotalAmount = request.TotalAmount,
            CreatedAt = DateTime.UtcNow
        };

        if (request.Categories is not null)
        {
            foreach (var cat in request.Categories)
            {
                var budgetCategoryId = Guid.NewGuid().ToString();
                budget.BudgetCategories.Add(new BudgetCategory
                {
                    BudgetCategoryId = budgetCategoryId,
                    BudgetId = budget.BudgetId,
                    CategoryId = cat.CategoryId,
                    AllocatedAmount = cat.AllocatedAmount,
                    AlertThreshold = cat.AlertThreshold,
                    SortOrder = cat.SortOrder,
                    Snapshot = new BudgetSnapshot
                    {
                        BudgetCategoryId = budgetCategoryId,
                        UpdatedAt = DateTime.UtcNow
                    }
                });
            }
        }

        var created = await _repository.CreateAsync(budget);
        return MapToBudgetDto(created);
    }

    public async Task<BudgetDto?> UpdateBudgetAsync(Guid userId, string budgetId, UpdateBudgetRequest request)
    {
        var budget = await _repository.GetByIdAsync(userId.ToString(), budgetId);
        if (budget is null) return null;

        budget.TotalAmount = request.TotalAmount;
        budget.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(budget);

        var rangeStart = DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
        var rangeEnd = DateOnly.ParseExact(budget.EndDate, "yyyy-MM-dd");
        await _repository.InvalidateCacheForBudgetRangeAsync(userId.ToString(), rangeStart, rangeEnd);

        return MapToBudgetDto(updated);
    }

    public async Task<BudgetCategoryDto?> AddCategoryAsync(Guid userId, string budgetId, CreateBudgetCategoryRequest request)
    {
        var budget = await _repository.GetByIdAsync(userId.ToString(), budgetId);
        if (budget is null) return null;

        // Prevent duplicate category in the same budget
        var duplicate = budget.BudgetCategories
            .Any(bc => bc.CategoryId == request.CategoryId);
        if (duplicate) return null;

        var budgetCategoryId = Guid.NewGuid().ToString();
        var budgetCategory = new Domain.Entities.BudgetCategory
        {
            BudgetCategoryId = budgetCategoryId,
            BudgetId = budgetId,
            CategoryId = request.CategoryId,
            AllocatedAmount = request.AllocatedAmount,
            AlertThreshold = request.AlertThreshold,
            SortOrder = request.SortOrder,
            Snapshot = new Domain.Entities.BudgetSnapshot
            {
                BudgetCategoryId = budgetCategoryId,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var created = await _repository.AddCategoryAsync(budgetCategory);
        return MapToCategoryDto(created);
    }

    public async Task<BudgetCategoryDto?> UpdateCategoryAllocationAsync(Guid userId, string budgetCategoryId, UpdateBudgetCategoryRequest request)
    {
        var budgetCategory = await _repository.GetBudgetCategoryAsync(userId.ToString(), budgetCategoryId);
        if (budgetCategory is null) return null;

        var rangeStart = DateOnly.ParseExact(budgetCategory.Budget!.StartDate, "yyyy-MM-dd");
        var rangeEnd = DateOnly.ParseExact(budgetCategory.Budget.EndDate, "yyyy-MM-dd");

        budgetCategory.AllocatedAmount = request.AllocatedAmount;
        if (request.AlertThreshold.HasValue)
            budgetCategory.AlertThreshold = request.AlertThreshold.Value;

        await _repository.UpdateBudgetCategoryAsync(budgetCategory);
        await _repository.InvalidateCacheForBudgetRangeAsync(userId.ToString(), rangeStart, rangeEnd);

        return MapToCategoryDto(budgetCategory);
    }

    public async Task<bool> RemoveCategoryAsync(Guid userId, string budgetCategoryId)
    {
        return await _repository.RemoveCategoryAsync(userId.ToString(), budgetCategoryId);
    }

    public async Task<bool> ResetBudgetAsync(Guid userId, string budgetId)
    {
        var budget = await _repository.GetByIdAsync(userId.ToString(), budgetId);
        if (budget is null) return false;

        await _repository.ResetSnapshotsAsync(userId.ToString(), budgetId);

        var rangeStart = DateOnly.ParseExact(budget.StartDate, "yyyy-MM-dd");
        var rangeEnd = DateOnly.ParseExact(budget.EndDate, "yyyy-MM-dd");
        await _repository.InvalidateCacheForBudgetRangeAsync(userId.ToString(), rangeStart, rangeEnd);

        return true;
    }

    public async Task<bool> DeleteBudgetAsync(Guid userId, string budgetId) =>
        await _repository.DeleteAsync(userId.ToString(), budgetId);

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static bool IsFullCalendarMonth(DateOnly start, DateOnly end) =>
        start.Day == 1 && end == start.AddMonths(1).AddDays(-1);

    private static string GetBudgetCategoryStatus(decimal spent, decimal allocated, decimal threshold)
    {
        if (allocated == 0) return "SAFE";
        var usage = spent / allocated;
        if (usage < threshold) return "SAFE";
        if (usage < 1.0m) return "WARNING";
        return "OVER";
    }

    private static BudgetCategoryDto MapToCategoryDto(BudgetCategory bc)
    {
        var spent = bc.Snapshot?.SpentAmount ?? 0;
        var remaining = bc.AllocatedAmount - spent;
        var usagePercent = bc.AllocatedAmount > 0
            ? (int)Math.Round(spent / bc.AllocatedAmount * 100)
            : 0;

        return new BudgetCategoryDto(
            bc.BudgetCategoryId,
            bc.CategoryId,
            bc.Category?.DisplayName ?? string.Empty,
            bc.Category?.Icon ?? string.Empty,
            bc.Category?.Color ?? string.Empty,
            bc.AllocatedAmount,
            spent,
            remaining,
            usagePercent,
            GetBudgetCategoryStatus(spent, bc.AllocatedAmount, bc.AlertThreshold),
            bc.AlertThreshold,
            bc.SortOrder);
    }

    private static BudgetDto MapToBudgetDto(Budget b) =>
        new(b.BudgetId, b.PeriodType.ToString().ToUpperInvariant(),
            b.StartDate, b.EndDate,
            b.TotalAmount, b.Status.ToString().ToUpperInvariant(), b.CreatedAt);
}
