
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Interfaces;

public interface ITranactionRepository
{
    Task<(List<Transaction> Items, int TotalCount)> GetTransactionsAsync(
        string userId,
        DateTime? startDate,
        DateTime? endDate,
        AppConstants.TransactionType? type,
        AppConstants.PaymentStatus? status,
        string? categoryId,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        string? cursorId);
    Task<Transaction?> GetByIdAsync(Guid userId, Guid tranactionId);
    Task<Transaction> CreateAsync(Transaction tranaction);
    Task CreateBatchAsync(List<Transaction> transactions);
    Task<Transaction?> UpdateAsync(Transaction tranaction);
    Task<bool> DeleteAsync(Guid userId, Guid tranactionId);
}
