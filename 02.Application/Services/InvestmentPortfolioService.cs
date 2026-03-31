using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;

namespace expense_tracker_backend.Application.Services;

public class InvestmentPortfolioService : IInvestmentPortfolioService
{
    private readonly IInvestmentPortfolioRepository _repository;

    public InvestmentPortfolioService(IInvestmentPortfolioRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<InvestmentPortfolioDto>> GetAllAsync(Guid userId)
    {
        var portfolios = await _repository.GetAllAsync(userId);
        return portfolios.Select(MapToDto).ToList();
    }

    public async Task<InvestmentPortfolioDto?> GetByIdAsync(Guid userId, Guid portfolioId)
    {
        var portfolio = await _repository.GetByIdAsync(userId, portfolioId);
        return portfolio is null ? null : MapToDto(portfolio);
    }

    public async Task<InvestmentPortfolioDto> CreateAsync(Guid userId, CreateInvestmentPortfolioRequest request)
    {
        var portfolio = new InvestmentPortfolio
        {
            PortfolioId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(portfolio);
        return MapToDto(created);
    }

    public async Task<InvestmentPortfolioDto?> UpdateAsync(Guid userId, Guid portfolioId, UpdateInvestmentPortfolioRequest request)
    {
        var updated = await _repository.UpdateAsync(new InvestmentPortfolio
        {
            PortfolioId = portfolioId.ToString(),
            UserId = userId.ToString(),
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive
        });

        return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid portfolioId)
    {
        return await _repository.DeleteAsync(userId, portfolioId);
    }

    private static InvestmentPortfolioDto MapToDto(InvestmentPortfolio p) =>
        new(Guid.Parse(p.PortfolioId),
            Guid.Parse(p.UserId),
            p.Name,
            p.Description,
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt);
}
