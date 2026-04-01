using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;

namespace expense_tracker_backend.Application.Services;

public class TranactionService : ITranactionService
{
    private readonly ITranactionRepository _repository;
    private readonly IAggregationRepository _aggregationRepository;
    private readonly IBudgetRepository _budgetRepository;

    public TranactionService(
        ITranactionRepository repository,
        IAggregationRepository aggregationRepository,
        IBudgetRepository budgetRepository)
    {
        _repository = repository;
        _aggregationRepository = aggregationRepository;
        _budgetRepository = budgetRepository;
    }

    public async Task<PagedResult<DTOs.Tranaction>> GetTransactionsAsync(Guid userId, TransactionFilterRequest filter)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        // This endpoint only serves Income and Expense — never Savings or Investment mirror rows
        var allowed = new[] {
            Domain.Shared.Constants.AppConstants.TransactionType.Income,
            Domain.Shared.Constants.AppConstants.TransactionType.Expense
        };
        var types = filter.Type.HasValue && allowed.Contains(filter.Type.Value)
            ? new[] { filter.Type.Value }
            : allowed;

        var (items, totalCount) = await _repository.GetTransactionsAsync(
            userId.ToString(),
            filter.StartDate,
            filter.EndDate,
            types,
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
            Status = dto.status,
            TransactionDate = dto.TranactionDate,
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow,
            Notes = dto.Note
        };
        var created = await _repository.CreateAsync(tranaction);

        await _aggregationRepository.UpdateRedisCacheAsync(created);

        // Budget snapshots only track expense transactions
        if (created.Type == Domain.Shared.Constants.AppConstants.TransactionType.Expense)
            await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                created.UserId, created.CategoryId, created.TransactionDate, created.Amount, 1);

        return MapToDto(created);
    }

    public async Task<DTOs.Tranaction?> UpdateTranactionAsync(Guid userId, Guid tranactionId, UpdateTranactionDto dto)
    {
        var existing = await _repository.GetByIdAsync(userId, tranactionId);
        if (existing is null) return null;

        var oldType = existing.Type;
        var oldCategoryId = existing.CategoryId;
        var oldAmount = existing.Amount;
        var oldDate = existing.TransactionDate;

        existing.Type = dto.type;
        existing.CategoryId = dto.CategoryId;
        existing.Amount = dto.Amount;
        existing.Description = dto.Description;
        existing.Status = dto.status;
        existing.TransactionDate = dto.TranactionDate;
        existing.ImageUrl = dto.ImageUrl;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Notes = dto.Note;
        var updated = await _repository.UpdateAsync(existing);
        if (updated is null) return null;

        await _aggregationRepository.UpdateRedisCacheAsync(updated);

        // Reverse old snapshot entry
        var expense = Domain.Shared.Constants.AppConstants.TransactionType.Expense;
        if (oldType == expense && oldCategoryId is not null)
            await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                updated.UserId, oldCategoryId, oldDate, -oldAmount, -1);

        if (updated.Type == expense && updated.CategoryId is not null)
            await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                updated.UserId, updated.CategoryId, updated.TransactionDate, updated.Amount, 1);

        return MapToDto(updated);
    }

    public async Task<bool> DeleteTranactionAsync(Guid userId, Guid tranactionId)
    {
        var existing = await _repository.GetByIdAsync(userId, tranactionId);
        if (existing is null) return false;

        var deleted = await _repository.DeleteAsync(userId, tranactionId);
        if (deleted)
        {
            await _aggregationRepository.UpdateRedisCacheAsync(existing);

            if (existing.Type == Domain.Shared.Constants.AppConstants.TransactionType.Expense)
                await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                    existing.UserId, existing.CategoryId, existing.TransactionDate, -existing.Amount, -1);
        }

        return deleted;
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
            expense.Status,
            expense.TransactionDate,
            expense.ImageUrl,
            expense.CreatedAt,
            expense.UpdatedAt,
            expense.Notes
        );
    }
}
