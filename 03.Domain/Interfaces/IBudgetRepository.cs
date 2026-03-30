using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(string userId, string budgetId);
    Task<Budget?> GetByMonthAsync(string userId, int year, int month);
    Task<BudgetCategory?> GetBudgetCategoryAsync(string userId, string budgetCategoryId);
    Task<Budget> CreateAsync(Budget budget);
    Task<Budget> UpdateAsync(Budget budget);
    Task<bool> DeleteAsync(string userId, string budgetId);
    Task<BudgetCategory> AddCategoryAsync(BudgetCategory budgetCategory);
    Task<BudgetCategory> UpdateBudgetCategoryAsync(BudgetCategory budgetCategory);
    Task<bool> RemoveCategoryAsync(string userId, string budgetCategoryId);
    Task UpdateSnapshotOnTransactionAsync(string userId, string categoryId, string transactionDate, decimal amountDelta, int countDelta);
    Task ResetSnapshotsAsync(string userId, string budgetId);
    Task InvalidateCacheAsync(string userId, int year, int month);
}
