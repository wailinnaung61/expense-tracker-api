using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface ISavingGoalContributionRepository
{
    Task<(List<SavingGoalContribution> Items, int TotalCount)> GetByGoalIdAsync(Guid userId, Guid savingGoalId, int pageSize, DateTime? cursor, Guid? cursorId);
    Task<List<SavingGoalContribution>> GetAllByGoalIdAsync(Guid userId, Guid savingGoalId);
    Task<SavingGoalContribution?> GetByIdAsync(Guid userId, Guid contributionId);
    Task<SavingGoalContribution> CreateAsync(SavingGoalContribution contribution);
    Task<bool> DeleteAsync(Guid userId, Guid contributionId);
}
