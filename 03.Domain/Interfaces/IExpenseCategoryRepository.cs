using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IExpenseCategoryRepository
{
    Task<(List<ExpenseCategory> Items, int TotalCount)> GetCategoriesAsync(
        string userId,
        DateTime? startDate,
        DateTime? endDate,
        AppConstants.TransactionType? type,
        string? categoryId,
        string? keyword,
        bool? isActive,
        int pageSize,
        DateTime? cursor,
        string? cursorId);
    Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid userId, Guid expenseCategoryId);
    Task<ExpenseCategory> CreateExpenseCategoryAsync(ExpenseCategory category);
    Task<ExpenseCategory?> UpdateExpenseCategoryAsync(ExpenseCategory category);
    Task<bool> DeleteExpenseCategoryAsync(Guid userId, Guid expenseCategoryId);
    Task SeedDefaultDataAsync(string userId);
    Task<bool> HasCategoriesAsync(string userId);
}
