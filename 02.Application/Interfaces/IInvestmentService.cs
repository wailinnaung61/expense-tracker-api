using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IInvestmentService
{
    Task<PagedResult<InvestmentDto>> GetInvestmentsAsync(Guid userId, InvestmentFilterRequest filter);
    Task<InvestmentDto?> GetByIdAsync(Guid userId, Guid investmentId);
    Task<InvestmentDto> CreateAsync(Guid userId, CreateInvestmentRequest request);
    Task<InvestmentDto?> UpdateAsync(Guid userId, Guid investmentId, UpdateInvestmentRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid investmentId);
    Task<InvestmentDashboardResponse> GetDashboardAsync(Guid userId);
}
