using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IBudgetService
{
    Task<BudgetMonthlyResponse?> GetByMonthAsync(Guid userId, int year, int month);

    /// <summary>Returns the budget whose period contains the given calendar day (e.g. pay-cycle view for May 20).</summary>
    Task<BudgetMonthlyResponse?> GetByContainingDateAsync(Guid userId, string date);

    Task<BudgetMonthlyResponse?> GetByDateRangeAsync(Guid userId, string startDate, string endDate);
    Task<BudgetDto> CreateBudgetAsync(Guid userId, CreateBudgetRequest request);
    Task<BudgetDto?> UpdateBudgetAsync(Guid userId, string budgetId, UpdateBudgetRequest request);
    Task<BudgetCategoryDto?> AddCategoryAsync(Guid userId, string budgetId, CreateBudgetCategoryRequest request);
    Task<BudgetCategoryDto?> UpdateCategoryAllocationAsync(Guid userId, string budgetCategoryId, UpdateBudgetCategoryRequest request);
    Task<bool> RemoveCategoryAsync(Guid userId, string budgetCategoryId);
    Task<bool> ResetBudgetAsync(Guid userId, string budgetId);
    Task<bool> DeleteBudgetAsync(Guid userId, string budgetId);
}
