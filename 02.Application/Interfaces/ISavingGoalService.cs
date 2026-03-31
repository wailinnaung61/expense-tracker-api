using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Interfaces;

public interface ISavingGoalService
{
    Task<SavingGoalDto?> GetSavingGoalByIdAsync(string userId, string savingGoalId);
    Task<List<SavingGoalDto>> GetSavingGoalsByUserIdAsync(string userId);
    Task<List<SavingGoalDto>> GetSavingGoalsByCategoryIdAsync(string userId, string categoryId);
    Task<List<SavingGoalDto>> GetSavingGoalsByStatusAsync(string userId, AppConstants.RecurringStatus status);
    Task<SavingGoalDto> CreateSavingGoalAsync(string userId, CreateSavingGoalDto dto);
    Task<SavingGoalDto?> UpdateSavingGoalAsync(string userId, string savingGoalId, UpdateSavingGoalDto dto);
    Task<SavingGoalDto?> PatchStatusAsync(string userId, string savingGoalId, AppConstants.RecurringStatus status);
    Task<bool> DeleteSavingGoalAsync(string userId, string savingGoalId);

    // Contributions (Add Funds)
    Task<SavingGoalContributionDto> AddContributionAsync(string userId, string savingGoalId, AddContributionDto dto);
    Task<List<SavingGoalContributionDto>> GetContributionsAsync(string userId, string savingGoalId);
}
