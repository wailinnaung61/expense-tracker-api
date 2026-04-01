using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IInvestmentRepository
{
    Task<(List<Investment> Items, int TotalCount)> GetInvestmentsAsync(
        string userId,
        string? portfolioId,
        AppConstants.AssetType? assetType,
        AppConstants.InvestmentStatus? status,
        string? keyword,
        int pageSize,
        DateTime? cursor,
        string? cursorId);
    Task<Investment?> GetByIdAsync(Guid userId, Guid investmentId);
    Task<Investment> CreateAsync(Investment investment);
    Task<Investment?> UpdateAsync(Investment investment);
    Task<bool> DeleteAsync(Guid userId, Guid investmentId);
    Task InvalidateCacheAsync(string userId);
    Task<List<Investment>> GetAllForDashboardAsync(string userId);
}
