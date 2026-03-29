using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Interfaces;

public interface ISavingGoalService
{
    Task<SavingGoalDto?> GetSavingGoalByIdAsync(string userId, string savingGoalId);
    Task<List<SavingGoalDto>> GetSavingGoalsByUserIdAsync(string userId);
    Task<List<SavingGoalDto>> GetSavingGoalsByCategoryIdAsync(string userId, string categoryId);
    Task<List<SavingGoalDto>> GetSavingGoalsByStatusAsync(string userId, AppConstants.RecurringStatus status);
    Task<SavingGoalDto> CreateSavingGoalAsync(string userId, SavingGoalDto savingGoalDto);
    Task<SavingGoalDto?> UpdateSavingGoalAsync(string userId, string savingGoalId, SavingGoalDto savingGoalDto);
    Task<bool> DeleteSavingGoalAsync(string userId, string savingGoalId);
}
