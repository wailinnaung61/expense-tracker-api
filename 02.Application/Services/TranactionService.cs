using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;

namespace expense_tracker_backend.Application.Services;

public class TranactionService : ITranactionService
{
    private readonly ITranactionRepository _repository;
    private readonly IAggregationRepository _aggregationRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly INotificationService _notificationService;
    private readonly IMemberRepository _memberRepository;
    private readonly IExpenseCategoryRepository _categoryRepository;

    public TranactionService(
        ITranactionRepository repository,
        IAggregationRepository aggregationRepository,
        IBudgetRepository budgetRepository,
        INotificationService notificationService,
        IMemberRepository memberRepository,
        IExpenseCategoryRepository categoryRepository)
    {
        _repository = repository;
        _aggregationRepository = aggregationRepository;
        _budgetRepository = budgetRepository;
        _notificationService = notificationService;
        _memberRepository = memberRepository;
        _categoryRepository = categoryRepository;
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

        // Budget snapshots only track completed expense transactions
        if (created.Type == Domain.Shared.Constants.AppConstants.TransactionType.Expense
            && created.Status == Domain.Shared.Constants.AppConstants.PaymentStatus.Completed)
        {
            var result = await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                created.UserId, created.CategoryId, created.TransactionDate, created.Amount, 1);
            await CheckBudgetAlertAsync(userId, result);
        }

        // Notify on payment failure
        if (created.Status == Domain.Shared.Constants.AppConstants.PaymentStatus.Failed)
            await _notificationService.NotifyPaymentFailedAsync(
                userId, await ResolveTransactionLabelAsync(userId, created),
                created.Amount.ToString("N0"), created.TransactionId);

        // Notify if single expense exceeds user's daily spending limit
        if (created.Type == Domain.Shared.Constants.AppConstants.TransactionType.Expense
            && created.Status == Domain.Shared.Constants.AppConstants.PaymentStatus.Completed)
            await CheckDailyLimitAsync(userId, created);

        return MapToDto(created);
    }

    public async Task<DTOs.Tranaction?> UpdateTranactionAsync(Guid userId, Guid tranactionId, UpdateTranactionDto dto)
    {
        var existing = await _repository.GetByIdAsync(userId, tranactionId);
        if (existing is null) return null;

        var oldType = existing.Type;
        var oldStatus = existing.Status;
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

        await _aggregationRepository.UpdateRedisCacheAsync(updated, oldDate != updated.TransactionDate ? oldDate : null);

        // Reverse old snapshot entry (only if it was a completed expense)
        var expense = Domain.Shared.Constants.AppConstants.TransactionType.Expense;
        var completed = Domain.Shared.Constants.AppConstants.PaymentStatus.Completed;
        if (oldType == expense && oldStatus == completed && oldCategoryId is not null)
            await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                updated.UserId, oldCategoryId, oldDate, -oldAmount, -1);

        // Add new snapshot entry (only if it is a completed expense)
        if (updated.Type == expense && updated.Status == completed && updated.CategoryId is not null)
        {
            var result = await _budgetRepository.UpdateSnapshotOnTransactionAsync(
                updated.UserId, updated.CategoryId, updated.TransactionDate, updated.Amount, 1);
            await CheckBudgetAlertAsync(userId, result);
        }

        // Notify when status changes to Failed
        var failed = Domain.Shared.Constants.AppConstants.PaymentStatus.Failed;
        if (oldStatus != failed && updated.Status == failed)
            await _notificationService.NotifyPaymentFailedAsync(
                userId, await ResolveTransactionLabelAsync(userId, updated),
                updated.Amount.ToString("N0"), updated.TransactionId);

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

            if (existing.Type == Domain.Shared.Constants.AppConstants.TransactionType.Expense
                && existing.Status == Domain.Shared.Constants.AppConstants.PaymentStatus.Completed)
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

    private async Task CheckBudgetAlertAsync(Guid userId, Domain.Interfaces.BudgetSnapshotResult? result)
    {
        if (result is null || result.AllocatedAmount <= 0 || !result.AlertsEnabled) return;

        var percent = (int)(result.SpentAmount / result.AllocatedAmount * 100);
        var spent = result.SpentAmount.ToString("N0");
        var allocated = result.AllocatedAmount.ToString("N0");

        if (result.SpentAmount > result.AllocatedAmount)
        {
            await _notificationService.NotifyBudgetExceededAsync(
                userId, result.CategoryName, spent, allocated, result.BudgetCategoryId);
        }
        else if (result.SpentAmount >= result.AllocatedAmount * result.AlertThreshold)
        {
            await _notificationService.NotifyBudgetThresholdAsync(
                userId, result.CategoryName, percent, spent, allocated, result.BudgetCategoryId);
        }
    }

    private async Task CheckDailyLimitAsync(Guid userId, Domain.Entities.Transaction tx)
    {
        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());
        if (profile is null || profile.DailyLimit <= 0) return;

        if (tx.Amount >= profile.DailyLimit)
        {
            await _notificationService.NotifyLargeTransactionAsync(
                userId, tx.Amount.ToString("N0"),
                await ResolveTransactionLabelAsync(userId, tx),
                tx.TransactionId);
        }
    }

    /// <summary>
    /// Prefer description; if empty, category name; never return blank (avoids email text like for "").
    /// </summary>
    private async Task<string> ResolveTransactionLabelAsync(Guid userId, Domain.Entities.Transaction tx)
    {
        if (!string.IsNullOrWhiteSpace(tx.Description))
            return tx.Description.Trim();

        if (!string.IsNullOrWhiteSpace(tx.Notes))
            return tx.Notes.Trim();

        if (!string.IsNullOrWhiteSpace(tx.CategoryId)
            && Guid.TryParse(tx.CategoryId, out var categoryId))
        {
            var category = await _categoryRepository.GetExpenseCategoryByIdAsync(userId, categoryId);
            if (!string.IsNullOrWhiteSpace(category?.DisplayName))
                return category.DisplayName.Trim();
        }

        return "Untitled expense";
    }
}
