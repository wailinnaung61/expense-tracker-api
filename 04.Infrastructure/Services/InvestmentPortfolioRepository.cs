using System.Text.Json;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class InvestmentPortfolioRepository : IInvestmentPortfolioRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<InvestmentPortfolioRepository> _logger;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    public InvestmentPortfolioRepository(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<InvestmentPortfolioRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<InvestmentPortfolio>> GetAllAsync(Guid userId)
    {
        var cacheKey = $"investment:portfolio:list:{userId}";

        try
        {
            var bytes = await _cache.GetAsync(cacheKey);
            if (bytes is not null)
                return JsonSerializer.Deserialize<List<InvestmentPortfolio>>(bytes)!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}", cacheKey);
        }

        var portfolios = await _context.InvestmentPortfolios
            .AsNoTracking()
            .Where(p => p.UserId == userId.ToString())
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(portfolios);
            await _cache.SetAsync(cacheKey, bytes, CacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {Key}", cacheKey);
        }

        return portfolios;
    }

    public async Task<InvestmentPortfolio?> GetByIdAsync(Guid userId, Guid portfolioId)
    {
        return await _context.InvestmentPortfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId.ToString()
                                   && p.UserId == userId.ToString());
    }

    public async Task<InvestmentPortfolio> CreateAsync(InvestmentPortfolio portfolio)
    {
        portfolio.CreatedAt = DateTime.UtcNow;
        await _context.InvestmentPortfolios.AddAsync(portfolio);
        await _context.SaveChangesAsync();
        await InvalidateCacheAsync(portfolio.UserId);
        return portfolio;
    }

    public async Task<InvestmentPortfolio?> UpdateAsync(InvestmentPortfolio portfolio)
    {
        var existing = await _context.InvestmentPortfolios
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolio.PortfolioId
                                   && p.UserId == portfolio.UserId);

        if (existing is null) return null;

        existing.Name = portfolio.Name;
        existing.Description = portfolio.Description;
        existing.IsActive = portfolio.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await InvalidateCacheAsync(existing.UserId);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid portfolioId)
    {
        var portfolio = await _context.InvestmentPortfolios
            .FirstOrDefaultAsync(p => p.PortfolioId == portfolioId.ToString()
                                   && p.UserId == userId.ToString());

        if (portfolio is null) return false;

        _context.InvestmentPortfolios.Remove(portfolio);
        await _context.SaveChangesAsync();
        await InvalidateCacheAsync(userId.ToString());
        return true;
    }

    private async Task InvalidateCacheAsync(string userId)
    {
        try
        {
            await _cache.RemoveAsync($"investment:portfolio:list:{userId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Portfolio cache invalidation failed for user {UserId}", userId);
        }
    }
}
