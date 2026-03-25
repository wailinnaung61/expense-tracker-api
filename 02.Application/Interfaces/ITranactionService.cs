/*using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;
using expense_tracker_backend.Domain.Shared.Models;

namespace expense_tracker_backend.Application.Interfaces;

public interface ITranactionService
{
    // Query
    Task<PaginatedResult<Tranaction>> GetByDateRangeWithFiltersAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        AppConstants.TransactionType? type,
        AppConstants.PaymentStatus? status,
        Guid? categoryId,
        string? keyword,
        PaginationRequest pagination);
    Task<Tranaction?> GetTranactionByIdAsync(Guid userId, Guid tranactionId);
    Task<Tranaction> CreateTranactionAsync(CreateTranactionDto dto, Guid userId);
    Task<Tranaction?> UpdateTranactionAsync(Guid userId, Guid tranactionId, UpdateTranactionDto dto);
    Task<bool> DeleteTranactionAsync(Guid userId, Guid tranactionId);
}
*/