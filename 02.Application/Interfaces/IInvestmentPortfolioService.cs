using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IInvestmentPortfolioService
{
    Task<List<InvestmentPortfolioDto>> GetAllAsync(Guid userId);
    Task<InvestmentPortfolioDto?> GetByIdAsync(Guid userId, Guid portfolioId);
    Task<InvestmentPortfolioDto> CreateAsync(Guid userId, CreateInvestmentPortfolioRequest request);
    Task<InvestmentPortfolioDto?> UpdateAsync(Guid userId, Guid portfolioId, UpdateInvestmentPortfolioRequest request);
    Task<bool> DeleteAsync(Guid userId, Guid portfolioId);
}
