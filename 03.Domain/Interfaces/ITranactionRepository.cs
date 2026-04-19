
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Interfaces;

public interface ITranactionRepository
{
    Task<(List<Transaction> Items, int TotalCount)> GetTransactionsAsync(
        string userId,
        DateTime? startDate,
        DateTime? endDate,
        IReadOnlyList<AppConstants.TransactionType>? types,
        AppConstants.PaymentStatus? status,
        string? categoryId,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        string? cursorId);
    Task<Transaction?> GetByIdAsync(Guid userId, Guid tranactionId);

    /// <summary>
    /// Completed expenses in [startDateIso, endDateIso] (yyyy-MM-dd), grouped by category id.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetCompletedExpenseTotalsByCategoryAsync(
        string userId,
        string startDateIso,
        string endDateIso,
        IReadOnlyList<string> categoryIds);

    Task<Transaction> CreateAsync(Transaction tranaction);
    Task CreateBatchAsync(List<Transaction> transactions);
    Task<Transaction?> UpdateAsync(Transaction tranaction);
    Task<bool> DeleteAsync(Guid userId, Guid tranactionId);
}
