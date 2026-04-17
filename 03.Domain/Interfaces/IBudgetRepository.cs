using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(string userId, string budgetId);
    Task<Budget?> GetByMonthAsync(string userId, int year, int month);

    /// <summary>Budget whose inclusive [StartDate, EndDate] contains <paramref name="dateIso"/> (yyyy-MM-dd).</summary>
    Task<Budget?> GetContainingBudgetAsync(string userId, string dateIso);

    Task<Budget?> GetByDateRangeAsync(string userId, string startDateIso, string endDateIso);
    Task<BudgetCategory?> GetBudgetCategoryAsync(string userId, string budgetCategoryId);
    Task<Budget> CreateAsync(Budget budget);
    Task<Budget> UpdateAsync(Budget budget);
    Task<bool> DeleteAsync(string userId, string budgetId);
    Task<BudgetCategory> AddCategoryAsync(BudgetCategory budgetCategory);
    Task<BudgetCategory> UpdateBudgetCategoryAsync(BudgetCategory budgetCategory);
    Task<bool> RemoveCategoryAsync(string userId, string budgetCategoryId);
    Task<BudgetSnapshotResult?> UpdateSnapshotOnTransactionAsync(string userId, string categoryId, string transactionDate, decimal amountDelta, int countDelta);
    Task ResetSnapshotsAsync(string userId, string budgetId);
    Task InvalidateCacheAsync(string userId, int year, int month);

    /// <summary>True if any budget for the user overlaps [startDateIso, endDateIso] (inclusive ISO yyyy-MM-dd).</summary>
    Task<bool> HasOverlappingBudgetAsync(string userId, string startDateIso, string endDateIso, string? excludeBudgetId = null);

    /// <summary>Invalidates month-scoped budget cache keys for every calendar month touched by the inclusive range.</summary>
    Task InvalidateCacheForBudgetRangeAsync(string userId, DateOnly rangeStart, DateOnly rangeEnd);
}

public record BudgetSnapshotResult(
    string BudgetCategoryId,
    string CategoryName,
    decimal SpentAmount,
    decimal AllocatedAmount,
    decimal AlertThreshold
);
