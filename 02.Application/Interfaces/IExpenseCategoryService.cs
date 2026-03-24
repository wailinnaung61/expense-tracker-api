using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Interfaces;

public interface IExpenseCategoryService
{
    Task<PagedResult<ExpenseCategory>> GetCategoriesAsync(Guid userId, CategoryFilterRequest filter);
    Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid userId, Guid expenseCategoryId);
    Task<ExpenseCategory> CreateExpenseCategoryAsync(Guid userId, CreateExpenseCategoryDto category);
    Task<ExpenseCategory?> UpdateExpenseCategoryAsync(Guid userId, Guid expenseCategoryId, UpdateExpenseCategoryDto category);
    Task<bool> DeleteExpenseCategoryAsync(Guid userId, Guid expenseCategoryId);
}