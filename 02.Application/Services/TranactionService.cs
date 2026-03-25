/*using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using expense_tracker_backend.Domain.Shared.Models;

namespace expense_tracker_backend.Application.Services;

public class TranactionService : ITranactionService
{
    private readonly ITranactionRepository _repository;
    private readonly IExpenseCategoryRepository _categoryRepository;

    public TranactionService(ITranactionRepository repository, IExpenseCategoryRepository categoryRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
    }

    public async Task<PaginatedResult<DTOs.Tranaction>> GetByDateRangeWithFiltersAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        AppConstants.TransactionType? type,
        AppConstants.PaymentStatus? status,
        Guid? categoryId,
        string? keyword,
        PaginationRequest pagination)
    {
        var result = await _repository.GetByDateRangeWithFiltersAsync(userId, startDate, endDate, type, status, categoryId, keyword, pagination);

        return PaginatedResult<DTOs.Tranaction>.Create(
            result.Items.Select(MapToDto),
            result.TotalCount,
            result.PageNumber,
            result.PageSize
        );
    }

    public async Task<DTOs.Tranaction?> GetTranactionByIdAsync(Guid userId, Guid tranactionId)
    {
        var tranaction = await _repository.GetByIdAsync(userId, tranactionId);
        return tranaction is null ? null : MapToDto(tranaction);
    }

    public async Task<DTOs.Tranaction> CreateTranactionAsync(CreateTranactionDto dto, Guid userId)
    {
        var category = await _categoryRepository.GetExpenseCategoryByIdAsync(userId, Guid.Parse(dto.CategoryId));
        var tranaction = new Domain.Entities.Tranaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            type = dto.type,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Description = dto.Description,
            Merchant = string.Empty,
            PaymentMethod = string.Empty,
            status = dto.status,
            TransactionDate = DateTime.Parse(dto.TranactionDate),
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTime.UtcNow,
            Notes = dto.Note
        };
        var created = await _repository.CreateAsync(tranaction);
        return MapToDto(created);
    }

    public async Task<DTOs.Tranaction?> UpdateTranactionAsync(Guid userId, Guid tranactionId, UpdateTranactionDto dto)
    {
        // TODO: Fix after entity migration
        throw new NotImplementedException("Transaction update temporarily disabled during PostgreSQL migration");

        // var existing = await _repository.GetByIdAsync(userId, tranactionId);
        // var category = await _categoryRepository.GetExpenseCategoryByIdAsync(userId, Guid.Parse(dto.CategoryId));
        // if (existing is null) return null;
        // existing.TransactionId = tranactionId.ToString();
        // existing.UserId = userId.ToString();
        // existing.type = dto.type;
        // existing.CategoryId = dto.CategoryId;
        // existing.Amount = dto.Amount;
        // existing.Description = dto.Description;
        // existing.Merchant = string.Empty;
        // existing.PaymentMethod = string.Empty;
        // existing.status = dto.status;
        // existing.TransactionDate = DateTime.Parse(dto.TranactionDate);
        // existing.ImageUrl = dto.ImageUrl;
        // existing.UpdatedAt = DateTime.UtcNow;
        // existing.Notes = dto.Note;
        // var updated = await _repository.UpdateAsync(existing);
        // return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteTranactionAsync(Guid userId, Guid tranactionId)
    {
        return await _repository.DeleteAsync(userId, tranactionId);
    }

    private static DTOs.Tranaction MapToDto(Domain.Entities.Tranaction expense)
    {
        return new DTOs.Tranaction(
            Guid.Parse(expense.TransactionId),
            Guid.Parse(expense.UserId),
            expense.type,
            expense.CategoryId,
            string.Empty, // CategoryName - removed from entity
            expense.Amount,
            expense.Description,
            expense.Merchant,
            expense.PaymentMethod,
            expense.status,
            expense.TransactionDate.ToString("yyyy-MM-dd"),
            expense.ImageUrl,
            expense.CreatedAt,
            expense.UpdatedAt,
            expense.Notes
        );
    }
}
*/