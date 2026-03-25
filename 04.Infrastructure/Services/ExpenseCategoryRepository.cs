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
            .Where(c => c.UserId == userId);

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

        // Keyset pagination: seek past the cursor instead of OFFSET
        if (cursor.HasValue && !string.IsNullOrEmpty(cursorId))
        {
            query = query.Where(c =>
                c.CreatedAt < cursor.Value ||
                (c.CreatedAt == cursor.Value && string.Compare(c.CategoryId, cursorId) < 0));
        }

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.CategoryId)
            .Take(pageSize + 1) // fetch one extra to determine hasNextPage
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<ExpenseCategory> CreateExpenseCategoryAsync(ExpenseCategory category)
    {
        throw new NotImplementedException();
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

    public Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid userId, Guid expenseCategoryId)
    {
        throw new NotImplementedException();
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

        var transactions = DefaultTransactionSeeder.GetDefaultTransactions(userId, categories, now);
        await _context.Transactions.AddRangeAsync(transactions);

        await _context.SaveChangesAsync();
    }

    public Task<ExpenseCategory?> UpdateExpenseCategoryAsync(ExpenseCategory category)
    {
        throw new NotImplementedException();
    }
}
