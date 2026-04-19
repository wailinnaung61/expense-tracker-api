using System.Collections.Generic;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class TranactionRepository : ITranactionRepository
{
    private readonly ApplicationDbContext _context;

    public TranactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Transaction> Items, int TotalCount)> GetTransactionsAsync(
        string userId,
        DateTime? startDate,
        DateTime? endDate,
        IReadOnlyList<AppConstants.TransactionType>? types,
        AppConstants.PaymentStatus? status,
        string? categoryId,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        string? cursorId)
    {
        var query = _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (types is not null && types.Count > 0)
            query = query.Where(t => types.Contains(t.Type));

        if (startDate.HasValue)
            query = query.Where(t => string.Compare(t.TransactionDate, startDate.Value.ToString("yyyy-MM-dd")) >= 0);

        if (endDate.HasValue)
            query = query.Where(t => string.Compare(t.TransactionDate, endDate.Value.ToString("yyyy-MM-dd")) <= 0);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrEmpty(categoryId))
            query = query.Where(t => t.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(t => EF.Functions.ILike(t.Description, $"%{keyword}%")
                                  || EF.Functions.ILike(t.Notes, $"%{keyword}%"));

        var totalCount = await query.CountAsync();

        if (cursor.HasValue && !string.IsNullOrEmpty(cursorId))
        {
            query = query.Where(t =>
                t.CreatedAt < cursor.Value ||
                (t.CreatedAt == cursor.Value && string.Compare(t.TransactionId, cursorId) < 0));
        }

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.TransactionId)
            .Take(pageSize + 1)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Transaction?> GetByIdAsync(Guid userId, Guid tranactionId)
    {
        return await _context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TransactionId == tranactionId.ToString()
                                   && t.UserId == userId.ToString());
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetCompletedExpenseTotalsByCategoryAsync(
        string userId,
        string startDateIso,
        string endDateIso,
        IReadOnlyList<string> categoryIds)
    {
        if (categoryIds is null || categoryIds.Count == 0)
            return new Dictionary<string, decimal>();

        var rows = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId
                && t.Type == AppConstants.TransactionType.Expense
                && t.Status == AppConstants.PaymentStatus.Completed
                && t.CategoryId != null
                && categoryIds.Contains(t.CategoryId)
                && string.Compare(t.TransactionDate, startDateIso) >= 0
                && string.Compare(t.TransactionDate, endDateIso) <= 0)
            .GroupBy(t => t.CategoryId!)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        return rows.ToDictionary(r => r.CategoryId, r => r.Total);
    }

    public async Task<Transaction> CreateAsync(Transaction tranaction)
    {
        tranaction.CreatedAt = DateTime.UtcNow;
        await _context.Transactions.AddAsync(tranaction);
        await _context.SaveChangesAsync();
        return tranaction;
    }

    public async Task CreateBatchAsync(List<Transaction> transactions)
    {
        await _context.Transactions.AddRangeAsync(transactions);
        await _context.SaveChangesAsync();
    }

    public async Task<Transaction?> UpdateAsync(Transaction tranaction)
    {
        var existing = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TransactionId == tranaction.TransactionId
                                   && t.UserId == tranaction.UserId);

        if (existing == null)
            return null;

        existing.Type = tranaction.Type;
        existing.CategoryId = tranaction.CategoryId;
        existing.Amount = tranaction.Amount;
        existing.Description = tranaction.Description;
        existing.Status = tranaction.Status;
        existing.TransactionDate = tranaction.TransactionDate;
        existing.ImageUrl = tranaction.ImageUrl;
        existing.Notes = tranaction.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.Transactions.Update(existing);
        await _context.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid tranactionId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TransactionId == tranactionId.ToString()
                                   && t.UserId == userId.ToString());

        if (transaction == null)
            return false;

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        return true;
    }
}
