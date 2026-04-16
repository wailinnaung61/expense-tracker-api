using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private readonly ApplicationDbContext _context;

    public ExpenseCategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(List<ExpenseCategory> Items, int TotalCount)> GetCategoriesAsync(
        string userId,
        DateTime? startDate,
        DateTime? endDate,
        AppConstants.TransactionType? type,
        string? categoryId,
        string? keyword,
        bool? isActive,
        int pageSize,
        DateTime? cursor,
        string? cursorId)
    {
        var query = _context.ExpenseCategories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.IsActive);

        if (startDate.HasValue)
            query = query.Where(c => c.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(c => c.CreatedAt <= endDate.Value);

        if (type.HasValue)
            query = query.Where(c => c.Type == type.Value);

        if (!string.IsNullOrEmpty(categoryId))
            query = query.Where(c => c.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(c => EF.Functions.ILike(c.DisplayName, $"%{keyword}%"));

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();

        if (!string.IsNullOrEmpty(cursorId))
        {
            // Cursor pagination must use the same ordering keys as the final ORDER BY
            // to avoid duplicates across pages.
            var cursorItem = await _context.ExpenseCategories
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.CategoryId == cursorId)
                .Select(c => new { c.DisplayName, c.CategoryId })
                .FirstOrDefaultAsync();

            if (cursorItem is not null)
            {
                query = query.Where(c =>
                    string.Compare(c.DisplayName, cursorItem.DisplayName) > 0 ||
                    (c.DisplayName == cursorItem.DisplayName &&
                     string.Compare(c.CategoryId, cursorItem.CategoryId) > 0));
            }
        }

        var items = await query
            .OrderBy(c => c.DisplayName)
            .ThenBy(c => c.CategoryId)
            .Take(pageSize + 1)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<ExpenseCategory> CreateExpenseCategoryAsync(ExpenseCategory category)
    {
        category.CreatedAt = DateTime.UtcNow;
        await _context.ExpenseCategories.AddAsync(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<bool> DeleteExpenseCategoryAsync(Guid userId, Guid expenseCategoryId)
    {
        var category = await _context.ExpenseCategories
            .FirstOrDefaultAsync(c => c.CategoryId == expenseCategoryId.ToString()
                                   && c.UserId == userId.ToString()
                                   && c.IsActive);

        if (category == null)
            return false;

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;

        _context.ExpenseCategories.Update(category);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid userId, Guid expenseCategoryId)
    {
        return await _context.ExpenseCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == expenseCategoryId.ToString()
                                   && c.UserId == userId.ToString()
                                   && c.IsActive);
    }

    public async Task<bool> HasCategoriesAsync(string userId)
    {
        return await _context.ExpenseCategories.AnyAsync(c => c.UserId == userId);
    }

    public async Task SeedDefaultDataAsync(string userId)
    {
        if (await HasCategoriesAsync(userId))
            return;

        var now = DateTime.UtcNow;
        var categories = DefaultCategorySeeder.GetDefaultCategories(userId, now);

        await _context.ExpenseCategories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();
    }

    public async Task<ExpenseCategory?> UpdateExpenseCategoryAsync(ExpenseCategory category)
    {
        var existing = await _context.ExpenseCategories
            .FirstOrDefaultAsync(c => c.CategoryId == category.CategoryId
                                   && c.UserId == category.UserId
                                   && c.IsActive);

        if (existing == null)
            return null;

        existing.DisplayName = category.DisplayName;
        existing.Icon = category.Icon;
        existing.Color = category.Color;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.ExpenseCategories.Update(existing);
        await _context.SaveChangesAsync();

        return existing;
    }
}
