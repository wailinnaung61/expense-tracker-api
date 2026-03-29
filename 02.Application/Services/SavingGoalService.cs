using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services;

public class SavingGoalService : ISavingGoalService
{
    private readonly ISavingGoalRepository _repository;

    public SavingGoalService(ISavingGoalRepository repository)
    {
        _repository = repository;
    }

    public async Task<SavingGoalDto?> GetSavingGoalByIdAsync(string userId, string savingGoalId)
    {
        var savingGoal = await _repository.GetByIdAsync(userId, savingGoalId);
        return savingGoal is null ? null : MapToDto(savingGoal);
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

    public async Task<SavingGoalDto> CreateSavingGoalAsync(string userId, SavingGoalDto savingGoalDto)
    {
        var savingGoal = new SavingGoal
        {
            SavingGoalId = Guid.NewGuid().ToString(),
            UserId = userId,
            CategoryId = savingGoalDto.CategoryId,
            GoalName = savingGoalDto.GoalName,
            TargetAmount = savingGoalDto.TargetAmount,
            InitialDeposit = savingGoalDto.InitialDeposit,
            TargetDate = savingGoalDto.TargetDate,
            RecurringType = ParseRecurringType(savingGoalDto.RecurringType),
            Icon = savingGoalDto.Icon,
            Color = savingGoalDto.Color,
            Status = ParseStatus(savingGoalDto.Status),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(savingGoal);
        return MapToDto(created);
    }

    public async Task<SavingGoalDto?> UpdateSavingGoalAsync(string userId, string savingGoalId, SavingGoalDto savingGoalDto)
    {
        var existing = await _repository.GetByIdAsync(userId, savingGoalId);
        if (existing is null) return null;

        existing.GoalName = savingGoalDto.GoalName;
        existing.TargetAmount = savingGoalDto.TargetAmount;
        existing.InitialDeposit = savingGoalDto.InitialDeposit;
        existing.TargetDate = savingGoalDto.TargetDate;
        existing.RecurringType = ParseRecurringType(savingGoalDto.RecurringType);
        existing.Icon = savingGoalDto.Icon;
        existing.Color = savingGoalDto.Color;
        existing.Status = ParseStatus(savingGoalDto.Status);
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing);
        return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteSavingGoalAsync(string userId, string savingGoalId)
    {
        return await _repository.DeleteAsync(userId, savingGoalId);
    }

    private static SavingGoalDto MapToDto(SavingGoal savingGoal)
    {
        return new SavingGoalDto
        {
            SavingGoalId = savingGoal.SavingGoalId,
            UserId = savingGoal.UserId,
            CategoryId = savingGoal.CategoryId,
            GoalName = savingGoal.GoalName,
            TargetAmount = savingGoal.TargetAmount,
            InitialDeposit = savingGoal.InitialDeposit,
            TargetDate = savingGoal.TargetDate,
            RecurringType = savingGoal.RecurringType.ToString().ToUpperInvariant(),
            Icon = savingGoal.Icon,
            Color = savingGoal.Color,
            Status = savingGoal.Status.ToString().ToUpperInvariant(),
            CreatedAt = savingGoal.CreatedAt,
            UpdatedAt = savingGoal.UpdatedAt
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
