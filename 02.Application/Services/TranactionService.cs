using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;

namespace expense_tracker_backend.Application.Services;

public class TranactionService : ITranactionService
{
    private readonly ITranactionRepository _repository;

    public TranactionService(ITranactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<DTOs.Tranaction>> GetTransactionsAsync(Guid userId, TransactionFilterRequest filter)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var (items, totalCount) = await _repository.GetTransactionsAsync(
            userId.ToString(),
            filter.StartDate,
            filter.EndDate,
            filter.Type,
            filter.Status,
            filter.CategoryId?.ToString(),
            filter.Keyword,
            pageSize,
            filter.Cursor,
            filter.CursorId?.ToString());

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedResult<DTOs.Tranaction>
        {
            Items = resultItems.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageSize = pageSize,
            HasNextPage = hasNextPage,
            NextCursor = lastItem?.CreatedAt,
            NextCursorId = lastItem is not null ? Guid.Parse(lastItem.TransactionId) : null
        };
    }

    public async Task<DTOs.Tranaction?> GetTranactionByIdAsync(Guid userId, Guid tranactionId)
    {
        var tranaction = await _repository.GetByIdAsync(userId, tranactionId);
        return tranaction is null ? null : MapToDto(tranaction);
    }

    public async Task<DTOs.Tranaction> CreateTranactionAsync(CreateTranactionDto dto, Guid userId)
    {
        var tranaction = new Domain.Entities.Transaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            Type = dto.type,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Description = dto.Description,
            Merchant = string.Empty,
            PaymentMethod = string.Empty,
            Status = dto.status,
            TransactionDate = dto.TranactionDate,
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow,
            Notes = dto.Note
        };
        var created = await _repository.CreateAsync(tranaction);

        return MapToDto(created);
    }

    public async Task<DTOs.Tranaction?> UpdateTranactionAsync(Guid userId, Guid tranactionId, UpdateTranactionDto dto)
    {
        var existing = await _repository.GetByIdAsync(userId, tranactionId);
        if (existing is null) return null;

        existing.Type = dto.type;
        existing.CategoryId = dto.CategoryId;
        existing.Amount = dto.Amount;
        existing.Description = dto.Description;
        existing.Merchant = string.Empty;
        existing.PaymentMethod = string.Empty;
        existing.Status = dto.status;
        existing.TransactionDate = dto.TranactionDate;
        existing.ImageUrl = dto.ImageUrl;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Notes = dto.Note;
        var updated = await _repository.UpdateAsync(existing);
        if (updated is null) return null;

        return MapToDto(updated);
    }

    public async Task<bool> DeleteTranactionAsync(Guid userId, Guid tranactionId)
    {
        var existing = await _repository.GetByIdAsync(userId, tranactionId);
        if (existing is null) return false;

        return await _repository.DeleteAsync(userId, tranactionId);
    }

    private static DTOs.Tranaction MapToDto(Domain.Entities.Transaction expense)
    {
        return new DTOs.Tranaction(
            Guid.Parse(expense.TransactionId),
            Guid.Parse(expense.UserId),
            expense.Type,
            expense.CategoryId,
            string.Empty,
            expense.Amount,
            expense.Description,
            expense.Merchant,
            expense.PaymentMethod,
            expense.Status,
            expense.TransactionDate,
            expense.ImageUrl,
            expense.CreatedAt,
            expense.UpdatedAt,
            expense.Notes
        );
    }
}
