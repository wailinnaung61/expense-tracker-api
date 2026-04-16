using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface ISavingGoalService
{
    Task<PagedResult<SavingGoalDto>> GetGoalsAsync(Guid userId, SavingGoalFilterRequest filter);
    Task<SavingGoalDto?> GetByIdAsync(Guid userId, Guid savingGoalId);
    Task<SavingGoalDto> CreateAsync(Guid userId, CreateSavingGoalRequest request);
    Task<SavingGoalDto?> UpdateAsync(Guid userId, Guid savingGoalId, UpdateSavingGoalRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid savingGoalId);
    Task<SavingDashboardResponse> GetDashboardAsync(Guid userId);
    Task<SavingDashboardResponse> GetDashboardByRangeAsync(Guid userId, string startDate, string endDate);

    // Contributions
    Task<PagedResult<SavingGoalContributionDto>> GetContributionsAsync(Guid userId, Guid savingGoalId, int pageSize, DateTime? cursor, Guid? cursorId);
    Task<SavingGoalContributionDto> AddContributionAsync(Guid userId, Guid savingGoalId, AddSavingContributionRequest request);
    Task<bool> DeleteContributionAsync(Guid userId, Guid savingGoalId, Guid contributionId);
}
