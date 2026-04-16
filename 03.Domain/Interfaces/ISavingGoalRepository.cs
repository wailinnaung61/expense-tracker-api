using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Interfaces;

public interface ISavingGoalRepository
{
    Task<SavingGoal?> GetByIdAsync(Guid userId, Guid savingGoalId);
    Task<(List<SavingGoal> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId,
        AppConstants.SavingGoalStatus? status,
        AppConstants.SavingGoalType? goalType,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        Guid? cursorId);
    Task<List<SavingGoal>> GetAllForDashboardAsync(Guid userId);
    Task<List<SavingGoal>> GetAllForDashboardByRangeAsync(Guid userId, string startDate, string endDate);
    Task<SavingGoal> CreateAsync(SavingGoal savingGoal);
    Task<SavingGoal?> UpdateAsync(SavingGoal savingGoal);
    Task<bool> DeleteAsync(Guid userId, Guid savingGoalId);
    Task InvalidateCacheAsync(string userId);
}
