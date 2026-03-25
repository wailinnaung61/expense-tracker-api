using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;
namespace expense_tracker_backend.Application.Interfaces;

public interface ITranactionService
{
    Task<PagedResult<Tranaction>> GetTransactionsAsync(Guid userId, TransactionFilterRequest filter);
    Task<Tranaction?> GetTranactionByIdAsync(Guid userId, Guid tranactionId);
    Task<Tranaction> CreateTranactionAsync(CreateTranactionDto dto, Guid userId);
    Task<Tranaction?> UpdateTranactionAsync(Guid userId, Guid tranactionId, UpdateTranactionDto dto);
    Task<bool> DeleteTranactionAsync(Guid userId, Guid tranactionId);
}
