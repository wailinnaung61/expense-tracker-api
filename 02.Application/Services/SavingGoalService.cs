using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services;

public class SavingGoalService : ISavingGoalService
{
    private readonly ISavingGoalRepository _repository;
    private readonly ISavingGoalContributionRepository _contributionRepository;
    private readonly ITranactionRepository _transactionRepository;
    private readonly IAggregationRepository _aggregationRepository;
    private readonly INotificationService _notificationService;

    public SavingGoalService(
        ISavingGoalRepository repository,
        ISavingGoalContributionRepository contributionRepository,
        ITranactionRepository transactionRepository,
        IAggregationRepository aggregationRepository,
        INotificationService notificationService)
    {
        _repository = repository;
        _contributionRepository = contributionRepository;
        _transactionRepository = transactionRepository;
        _aggregationRepository = aggregationRepository;
        _notificationService = notificationService;
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<SavingGoalDto>> GetGoalsAsync(Guid userId, SavingGoalFilterRequest filter)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var (items, totalCount) = await _repository.GetByUserIdAsync(
            userId, filter.Status, filter.GoalType, filter.Keyword, pageSize, filter.Cursor, filter.CursorId);

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedResult<SavingGoalDto>
        {
            Items = resultItems.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageSize = pageSize,
            HasNextPage = hasNextPage,
            NextCursor = lastItem?.CreatedAt,
            NextCursorId = lastItem is not null ? Guid.Parse(lastItem.SavingGoalId) : null
        };
    }

    public async Task<SavingGoalDto?> GetByIdAsync(Guid userId, Guid savingGoalId)
    {
        var goal = await _repository.GetByIdAsync(userId, savingGoalId);
        return goal is null ? null : MapToDto(goal);
    }

    public async Task<SavingGoalDto> CreateAsync(Guid userId, CreateSavingGoalRequest request)
    {
        var goal = new SavingGoal
        {
            SavingGoalId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            GoalName = request.GoalName,
            Description = request.Description,
            TargetAmount = request.TargetAmount,
            CurrentAmount = 0,
            TargetDate = request.TargetDate,
            Status = AppConstants.SavingGoalStatus.Active,
            SavingGoalType = request.SavingGoalType,
            Notes = request.Notes,
            Icon = request.Icon,
            Color = request.Color,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(goal);
        await _repository.InvalidateCacheAsync(userId.ToString());
        return MapToDto(created);
    }

    public async Task<SavingGoalDto?> UpdateAsync(Guid userId, Guid savingGoalId, UpdateSavingGoalRequest request)
    {
        var existing = await _repository.GetByIdAsync(userId, savingGoalId);
        if (existing is null) return null;

        existing.GoalName = request.GoalName;
        existing.Description = request.Description;
        existing.TargetAmount = request.TargetAmount;
        existing.TargetDate = request.TargetDate;
        existing.Status = request.Status;
        existing.SavingGoalType = request.SavingGoalType;
        existing.Notes = request.Notes;
        existing.Icon = request.Icon;
        existing.Color = request.Color;

        // Auto-complete
        if (existing.CurrentAmount >= request.TargetAmount && request.Status == AppConstants.SavingGoalStatus.Active)
            existing.Status = AppConstants.SavingGoalStatus.Completed;

        var updated = await _repository.UpdateAsync(existing);
        if (updated is not null) await _repository.InvalidateCacheAsync(userId.ToString());
        return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid savingGoalId)
    {
        // Load all contributions to clean up mirror transactions
        var contributions = await _contributionRepository.GetAllByGoalIdAsync(userId, savingGoalId);

        foreach (var c in contributions.Where(c => c.MirrorTransactionId is not null))
        {
            var mirrorTx = await _transactionRepository.GetByIdAsync(userId, Guid.Parse(c.MirrorTransactionId!));
            if (mirrorTx is not null)
            {
                await _transactionRepository.DeleteAsync(userId, Guid.Parse(c.MirrorTransactionId!));
                await _aggregationRepository.UpdateRedisCacheAsync(mirrorTx);
            }
        }

        var deleted = await _repository.DeleteAsync(userId, savingGoalId);
        if (deleted) await _repository.InvalidateCacheAsync(userId.ToString());
        return deleted;
    }

    public async Task<SavingDashboardResponse> GetDashboardAsync(Guid userId)
    {
        var goals = await _repository.GetAllForDashboardAsync(userId);

        var totalSaved = goals.Sum(g => g.CurrentAmount);
        var totalTarget = goals
            .Where(g => g.Status == AppConstants.SavingGoalStatus.Active)
            .Sum(g => g.TargetAmount);
        var overallProgress = totalTarget > 0 ? Math.Round(totalSaved / totalTarget * 100, 2) : 0;

        var top5Goals = goals
            .Select(MapToDto)
            .Where(g => g.Status == AppConstants.SavingGoalStatus.Active.ToString().ToUpperInvariant())
            .OrderByDescending(g => g.ProgressPercentage)
            .Take(5)
            .ToList();

        return new SavingDashboardResponse(
            totalSaved,
            totalTarget,
            overallProgress,
            goals.Count(g => g.Status == AppConstants.SavingGoalStatus.Active),
            goals.Count(g => g.Status == AppConstants.SavingGoalStatus.Completed),
            top5Goals
        );
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    public async Task<PagedResult<SavingGoalContributionDto>> GetContributionsAsync(
        Guid userId, Guid savingGoalId, int pageSize, DateTime? cursor, Guid? cursorId)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, totalCount) = await _contributionRepository.GetByGoalIdAsync(userId, savingGoalId, pageSize, cursor, cursorId);

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedResult<SavingGoalContributionDto>
        {
            Items = resultItems.Select(MapContributionToDto).ToList(),
            TotalCount = totalCount,
            PageSize = pageSize,
            HasNextPage = hasNextPage,
            NextCursor = lastItem?.CreatedAt,
            NextCursorId = lastItem is not null ? Guid.Parse(lastItem.ContributionId) : null
        };
    }

    public async Task<SavingGoalContributionDto> AddContributionAsync(
        Guid userId, Guid savingGoalId, AddSavingContributionRequest request)
    {
        var goal = await _repository.GetByIdAsync(userId, savingGoalId)
            ?? throw new InvalidOperationException("Saving goal not found.");

        if (goal.Status == AppConstants.SavingGoalStatus.Cancelled)
            throw new InvalidOperationException("Cannot contribute to a cancelled goal.");

        if (request.Type == AppConstants.SavingTransactionType.Withdrawal
            && request.Amount > goal.CurrentAmount)
            throw new InvalidOperationException("Withdrawal amount exceeds current saved amount.");

        // Mirror to transactions: deposit = positive amount, withdrawal = negative
        var mirrorAmount = request.Type == AppConstants.SavingTransactionType.Deposit
            ? request.Amount
            : -request.Amount;

        var mirrorTx = new Transaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            Type = AppConstants.TransactionType.Savings,
            CategoryId = null,
            Amount = mirrorAmount,
            Description = $"Saving: {goal.GoalName} ({request.Type})",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = request.ContributionDate,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(mirrorTx);
        _ = _aggregationRepository.UpdateRedisCacheAsync(mirrorTx);

        var contribution = new SavingGoalContribution
        {
            ContributionId = Guid.NewGuid().ToString(),
            SavingGoalId = savingGoalId.ToString(),
            UserId = userId.ToString(),
            Type = request.Type,
            Amount = request.Amount,
            ContributionDate = request.ContributionDate,
            Notes = request.Notes,
            MirrorTransactionId = mirrorTx.TransactionId,
            CreatedAt = DateTime.UtcNow
        };
        await _contributionRepository.CreateAsync(contribution);

        // Update persisted CurrentAmount and auto-complete check
        goal.CurrentAmount = request.Type == AppConstants.SavingTransactionType.Deposit
            ? goal.CurrentAmount + request.Amount
            : goal.CurrentAmount - request.Amount;

        if (goal.CurrentAmount >= goal.TargetAmount && goal.Status == AppConstants.SavingGoalStatus.Active)
        {
            goal.Status = AppConstants.SavingGoalStatus.Completed;

            await _notificationService.NotifySavingGoalReachedAsync(
                userId, goal.GoalName, goal.SavingGoalId);
        }

        await _repository.UpdateAsync(goal);
        await _repository.InvalidateCacheAsync(userId.ToString());

        return MapContributionToDto(contribution);
    }

    public async Task<bool> DeleteContributionAsync(Guid userId, Guid savingGoalId, Guid contributionId)
    {
        var contribution = await _contributionRepository.GetByIdAsync(userId, contributionId);
        if (contribution is null || contribution.SavingGoalId != savingGoalId.ToString())
            return false;

        // Reverse the contribution's effect on CurrentAmount
        var goal = await _repository.GetByIdAsync(userId, savingGoalId);
        if (goal is not null)
        {
            goal.CurrentAmount = contribution.Type == AppConstants.SavingTransactionType.Deposit
                ? goal.CurrentAmount - contribution.Amount
                : goal.CurrentAmount + contribution.Amount;

            // Revert completed status if we pulled back funds below target
            if (goal.Status == AppConstants.SavingGoalStatus.Completed
                && goal.CurrentAmount < goal.TargetAmount)
                goal.Status = AppConstants.SavingGoalStatus.Active;

            await _repository.UpdateAsync(goal);
        }

        // Delete mirror transaction
        if (contribution.MirrorTransactionId is not null)
        {
            var mirrorTx = await _transactionRepository.GetByIdAsync(userId, Guid.Parse(contribution.MirrorTransactionId));
            if (mirrorTx is not null)
            {
                await _transactionRepository.DeleteAsync(userId, Guid.Parse(contribution.MirrorTransactionId));
                await _aggregationRepository.UpdateRedisCacheAsync(mirrorTx);
            }
        }

        var deleted = await _contributionRepository.DeleteAsync(userId, contributionId);
        if (deleted) await _repository.InvalidateCacheAsync(userId.ToString());
        return deleted;
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static SavingGoalDto MapToDto(SavingGoal g)
    {
        var progress = g.TargetAmount > 0
            ? Math.Round(g.CurrentAmount / g.TargetAmount * 100, 2)
            : 0;
        var remaining = Math.Max(0, g.TargetAmount - g.CurrentAmount);

        return new SavingGoalDto(
            Guid.Parse(g.SavingGoalId),
            Guid.Parse(g.UserId),
            g.GoalName,
            g.Description,
            g.TargetAmount,
            g.CurrentAmount,
            progress,
            remaining,
            g.TargetDate,
            g.Status.ToString().ToUpperInvariant(),
            g.SavingGoalType.ToString().ToUpperInvariant(),
            g.Notes,
            g.Icon,
            g.Color,
            g.CreatedAt,
            g.UpdatedAt
        );
    }

    private static SavingGoalContributionDto MapContributionToDto(SavingGoalContribution c) =>
        new(
            Guid.Parse(c.ContributionId),
            Guid.Parse(c.SavingGoalId),
            c.Type.ToString().ToUpperInvariant(),
            c.Amount,
            c.ContributionDate,
            c.Notes,
            c.CreatedAt
        );
}

