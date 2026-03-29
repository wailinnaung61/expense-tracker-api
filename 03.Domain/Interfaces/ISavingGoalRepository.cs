using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Interfaces;

public interface ISavingGoalRepository
{
    Task<SavingGoal?> GetByIdAsync(string userId, string savingGoalId);
    Task<List<SavingGoal>> GetByUserIdAsync(string userId);
    Task<List<SavingGoal>> GetByCategoryIdAsync(string userId, string categoryId);
    Task<List<SavingGoal>> GetByStatusAsync(string userId, AppConstants.RecurringStatus status);
    Task<SavingGoal> CreateAsync(SavingGoal savingGoal);
    Task<SavingGoal?> UpdateAsync(SavingGoal savingGoal);
    Task<bool> DeleteAsync(string userId, string savingGoalId);
}
