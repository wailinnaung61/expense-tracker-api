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

    public SavingGoalService(
        ISavingGoalRepository repository,
        ISavingGoalContributionRepository contributionRepository)
    {
        _repository = repository;
        _contributionRepository = contributionRepository;
    }

    public async Task<SavingGoalDto?> GetSavingGoalByIdAsync(string userId, string savingGoalId)
    {
        var savingGoal = await _repository.GetByIdAsync(userId, savingGoalId);
        if (savingGoal is null) return null;

        var dto = MapToDto(savingGoal);
        dto.Contributions = savingGoal.Contributions
            .OrderByDescending(c => c.ContributionDate)
            .Select(MapContributionToDto)
            .ToList();
        return dto;
    }

    public async Task<List<SavingGoalDto>> GetSavingGoalsByUserIdAsync(string userId)
    {
        var savingGoals = await _repository.GetByUserIdAsync(userId);
        return savingGoals.Select(MapToDto).ToList();
    }

    public async Task<List<SavingGoalDto>> GetSavingGoalsByCategoryIdAsync(string userId, string categoryId)
    {
        var savingGoals = await _repository.GetByCategoryIdAsync(userId, categoryId);
        return savingGoals.Select(MapToDto).ToList();
    }

    public async Task<List<SavingGoalDto>> GetSavingGoalsByStatusAsync(string userId, AppConstants.RecurringStatus status)
    {
        var savingGoals = await _repository.GetByStatusAsync(userId, status);
        return savingGoals.Select(MapToDto).ToList();
    }

    public async Task<SavingGoalDto> CreateSavingGoalAsync(string userId, CreateSavingGoalDto dto)
    {
        var savingGoal = new SavingGoal
        {
            SavingGoalId = Guid.NewGuid().ToString(),
            UserId = userId,
            CategoryId = dto.CategoryId,
            GoalName = dto.GoalName,
            TargetAmount = dto.TargetAmount,
            InitialDeposit = dto.InitialDeposit,
            TargetDate = dto.TargetDate,
            RecurringType = ParseRecurringType(dto.RecurringType),
            Icon = dto.Icon,
            Color = dto.Color,
            Status = AppConstants.RecurringStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(savingGoal);
        return MapToDto(created);
    }

    public async Task<SavingGoalDto?> UpdateSavingGoalAsync(string userId, string savingGoalId, UpdateSavingGoalDto dto)
    {
        var existing = await _repository.GetByIdAsync(userId, savingGoalId);
        if (existing is null) return null;

        existing.CategoryId = dto.CategoryId;
        existing.GoalName = dto.GoalName;
        existing.TargetAmount = dto.TargetAmount;
        existing.TargetDate = dto.TargetDate;
        existing.RecurringType = ParseRecurringType(dto.RecurringType);
        existing.Icon = dto.Icon;
        existing.Color = dto.Color;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing);
        return updated is null ? null : MapToDto(updated);
    }

    public async Task<SavingGoalDto?> PatchStatusAsync(string userId, string savingGoalId, AppConstants.RecurringStatus status)
    {
        var existing = await _repository.GetByIdAsync(userId, savingGoalId);
        if (existing is null) return null;

        existing.Status = status;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing);
        return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteSavingGoalAsync(string userId, string savingGoalId)
    {
        return await _repository.DeleteAsync(userId, savingGoalId);
    }

    public async Task<SavingGoalContributionDto> AddContributionAsync(string userId, string savingGoalId, AddContributionDto dto)
    {
        var contribution = new SavingGoalContribution
        {
            ContributionId = Guid.NewGuid().ToString(),
            SavingGoalId = savingGoalId,
            UserId = userId,
            Amount = dto.Amount,
            ContributionDate = dto.ContributionDate == default ? DateTime.UtcNow : dto.ContributionDate,
            Notes = dto.Notes ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _contributionRepository.CreateAsync(contribution);

        // Auto-complete goal if current amount reaches target
        var totalContributions = await _contributionRepository.GetTotalContributionsAsync(savingGoalId);
        var goal = await _repository.GetByIdAsync(userId, savingGoalId);
        if (goal is not null && goal.Status == AppConstants.RecurringStatus.Active)
        {
            var currentAmount = goal.InitialDeposit + totalContributions;
            if (currentAmount >= goal.TargetAmount)
            {
                goal.Status = AppConstants.RecurringStatus.Completed;
                goal.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(goal);
            }
        }

        return MapContributionToDto(created);
    }

    public async Task<List<SavingGoalContributionDto>> GetContributionsAsync(string userId, string savingGoalId)
    {
        var contributions = await _contributionRepository.GetByGoalIdAsync(userId, savingGoalId);
        return contributions.Select(MapContributionToDto).ToList();
    }

    private static SavingGoalDto MapToDto(SavingGoal savingGoal)
    {
        var contributionsTotal = savingGoal.Contributions.Sum(c => c.Amount);
        return new SavingGoalDto
        {
            SavingGoalId = savingGoal.SavingGoalId,
            UserId = savingGoal.UserId,
            CategoryId = savingGoal.CategoryId,
            GoalName = savingGoal.GoalName,
            TargetAmount = savingGoal.TargetAmount,
            InitialDeposit = savingGoal.InitialDeposit,
            CurrentAmount = savingGoal.InitialDeposit + contributionsTotal,
            TargetDate = savingGoal.TargetDate,
            RecurringType = savingGoal.RecurringType.ToString().ToUpperInvariant(),
            Icon = savingGoal.Icon,
            Color = savingGoal.Color,
            Status = savingGoal.Status.ToString().ToUpperInvariant(),
            CreatedAt = savingGoal.CreatedAt,
            UpdatedAt = savingGoal.UpdatedAt
        };
    }

    private static SavingGoalContributionDto MapContributionToDto(SavingGoalContribution c)
    {
        return new SavingGoalContributionDto
        {
            ContributionId = c.ContributionId,
            SavingGoalId = c.SavingGoalId,
            Amount = c.Amount,
            ContributionDate = c.ContributionDate,
            Notes = c.Notes,
            CreatedAt = c.CreatedAt
        };
    }

    private static AppConstants.RecurringFrequency ParseRecurringType(string recurringType)
    {
        return Enum.TryParse<AppConstants.RecurringFrequency>(recurringType, true, out var result)
            ? result
            : AppConstants.RecurringFrequency.Monthly;
    }

    private static AppConstants.RecurringStatus ParseStatus(string status)
    {
        return Enum.TryParse<AppConstants.RecurringStatus>(status, true, out var result)
            ? result
            : AppConstants.RecurringStatus.Active;
    }
}

