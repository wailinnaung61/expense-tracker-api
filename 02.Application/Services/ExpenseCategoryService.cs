using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IExpenseCategoryRepository _repository;

    public ExpenseCategoryService(IExpenseCategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<DTOs.ExpenseCategory>> GetCategoriesAsync(Guid userId, CategoryFilterRequest filter)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var (items, totalCount) = await _repository.GetCategoriesAsync(
            userId.ToString(),
            filter.StartDate,
            filter.EndDate,
            filter.Type,
            filter.CategoryId?.ToString(),
            filter.Keyword,
            filter.IsActive,
            pageSize,
            filter.Cursor,
            filter.CursorId?.ToString());

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedResult<DTOs.ExpenseCategory>
        {
            Items = resultItems.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageSize = pageSize,
            HasNextPage = hasNextPage,
            NextCursor = lastItem?.CreatedAt,
            NextCursorId = lastItem is not null ? Guid.Parse(lastItem.CategoryId) : null
        };
    }

    public async Task<DTOs.ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid userId, Guid expenseCategoryId)
    {
        var category = await _repository.GetExpenseCategoryByIdAsync(userId, expenseCategoryId);
        return category is null ? null : MapToDto(category);
    }

    public async Task<DTOs.ExpenseCategory> CreateExpenseCategoryAsync(Guid userId, CreateExpenseCategoryDto category)
    {
        if (category.Type is AppConstants.TransactionType.Investment or AppConstants.TransactionType.Savings)
            throw new InvalidOperationException("Categories can only be created for Income or Expense types.");

        var expenseCategory = new Domain.Entities.ExpenseCategory
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            DisplayName = category.DisplayName,
            Type = category.Type,
            Icon = category.Icon,
            Color = category.Color,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateExpenseCategoryAsync(expenseCategory);
        return MapToDto(created);
    }

    public async Task<DTOs.ExpenseCategory?> UpdateExpenseCategoryAsync(Guid userId, Guid expenseCategoryId, UpdateExpenseCategoryDto category)
    {
        var existing = await _repository.GetExpenseCategoryByIdAsync(userId, expenseCategoryId);
        if (existing is null) return null;

        existing.DisplayName = category.DisplayName;
        existing.Type = existing.Type;
        existing.Icon = category.Icon;
        existing.Color = category.Color;
        existing.CreatedAt = existing.CreatedAt;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateExpenseCategoryAsync(existing);
        return updated is null ? null : MapToDto(updated);
    }

    public Task<bool> DeleteExpenseCategoryAsync(Guid userId, Guid expenseCategoryId)
    {
        return _repository.DeleteExpenseCategoryAsync(userId, expenseCategoryId);
    }

    private static DTOs.ExpenseCategory MapToDto(Domain.Entities.ExpenseCategory category)
    {
        return new DTOs.ExpenseCategory(
            Guid.Parse(category.CategoryId),
            category.DisplayName,
            category.Type,
            category.Icon,
            category.Color,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt
        );
    }
}
