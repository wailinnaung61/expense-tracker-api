using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface ISavingGoalContributionRepository
{
    Task<List<SavingGoalContribution>> GetByGoalIdAsync(string userId, string savingGoalId);
    Task<SavingGoalContribution?> GetByIdAsync(string userId, string contributionId);
    Task<SavingGoalContribution> CreateAsync(SavingGoalContribution contribution);
    Task<decimal> GetTotalContributionsAsync(string savingGoalId);
}
