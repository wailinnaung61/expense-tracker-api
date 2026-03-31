using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IInvestmentPortfolioRepository
{
    Task<List<InvestmentPortfolio>> GetAllAsync(Guid userId);
    Task<InvestmentPortfolio?> GetByIdAsync(Guid userId, Guid portfolioId);
    Task<InvestmentPortfolio> CreateAsync(InvestmentPortfolio portfolio);
    Task<InvestmentPortfolio?> UpdateAsync(InvestmentPortfolio portfolio);
    Task<bool> DeleteAsync(Guid userId, Guid portfolioId);
}
