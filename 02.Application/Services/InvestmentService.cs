using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services;

public class InvestmentService : IInvestmentService
{
    private readonly IInvestmentRepository _repository;
    private readonly ITranactionRepository _transactionRepository;
    private readonly IAggregationRepository _aggregationRepository;

    public InvestmentService(
        IInvestmentRepository repository,
        ITranactionRepository transactionRepository,
        IAggregationRepository aggregationRepository)
    {
        _repository = repository;
        _transactionRepository = transactionRepository;
        _aggregationRepository = aggregationRepository;
    }

    public async Task<PagedResult<InvestmentDto>> GetInvestmentsAsync(Guid userId, InvestmentFilterRequest filter)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var (items, totalCount) = await _repository.GetInvestmentsAsync(
            userId.ToString(),
            filter.PortfolioId?.ToString(),
            filter.AssetType,
            filter.Status,
            filter.Keyword,
            pageSize,
            filter.Cursor,
            filter.CursorId?.ToString());

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedResult<InvestmentDto>
        {
            Items = resultItems.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageSize = pageSize,
            HasNextPage = hasNextPage,
            NextCursor = lastItem?.CreatedAt,
            NextCursorId = lastItem is not null ? Guid.Parse(lastItem.InvestmentId) : null
        };
    }

    public async Task<InvestmentDto?> GetByIdAsync(Guid userId, Guid investmentId)
    {
        var investment = await _repository.GetByIdAsync(userId, investmentId);
        return investment is null ? null : MapToDto(investment);
    }

    public async Task<InvestmentDto> CreateAsync(Guid userId, CreateInvestmentRequest request)
    {
        var investment = new Investment
        {
            InvestmentId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            PortfolioId = request.PortfolioId?.ToString(),
            AssetType = request.AssetType,
            AssetName = request.AssetName,
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            PurchasePrice = request.PurchasePrice,
            CurrentPrice = request.CurrentPrice,
            PurchaseDate = request.PurchaseDate,
            Notes = request.Notes,
            ImageUrl = request.ImageUrl,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(investment);

        // Mirror to transactions table so aggregation picks it up
        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            Type = AppConstants.TransactionType.Investment,
            CategoryId = null,
            Amount = created.Quantity * created.PurchasePrice,
            Description = $"Investment: {created.AssetName}",
            Merchant = string.Empty,
            PaymentMethod = string.Empty,
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = created.PurchaseDate,
            ImageUrl = created.ImageUrl,
            Notes = created.Notes,
            CreatedAt = DateTime.UtcNow
        };
        await _transactionRepository.CreateAsync(transaction);
        _ = _aggregationRepository.UpdateRedisCacheAsync(transaction);
        await _repository.InvalidateCacheAsync(userId.ToString());

        return MapToDto(created);
    }

    public async Task<InvestmentDto?> UpdateAsync(Guid userId, Guid investmentId, UpdateInvestmentRequest request)
    {
        var updated = await _repository.UpdateAsync(new Investment
        {
            InvestmentId = investmentId.ToString(),
            UserId = userId.ToString(),
            PortfolioId = request.PortfolioId?.ToString(),
            AssetType = AppConstants.AssetType.Other,  // not updated — only used as carrier
            AssetName = request.AssetName,
            Symbol = request.Symbol,
            Quantity = request.Quantity,
            PurchasePrice = request.PurchasePrice,
            CurrentPrice = request.CurrentPrice,
            PurchaseDate = request.PurchaseDate,
            Status = request.Status,
            Notes = request.Notes,
            ImageUrl = request.ImageUrl
        });

        if (updated is not null)
            await _repository.InvalidateCacheAsync(userId.ToString());

        return updated is null ? null : MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid investmentId)
    {
        var deleted = await _repository.DeleteAsync(userId, investmentId);
        if (deleted)
            await _repository.InvalidateCacheAsync(userId.ToString());
        return deleted;
    }

    public async Task<InvestmentDashboardResponse> GetDashboardAsync(Guid userId)
    {
        var items = await _repository.GetAllForDashboardAsync(userId.ToString());

        var totalInvested = items.Sum(i => i.Quantity * i.PurchasePrice);
        var currentValue = items.Sum(i => i.Quantity * i.CurrentPrice);
        var totalProfitLoss = currentValue - totalInvested;
        var returnPercentage = totalInvested > 0
            ? Math.Round(totalProfitLoss / totalInvested * 100, 2)
            : 0;

        var allocation = items
            .GroupBy(i => i.AssetType)
            .Select(g => new AssetAllocationDto(
                g.Key.ToString().ToUpperInvariant(),
                g.Sum(i => i.Quantity * i.CurrentPrice),
                currentValue > 0
                    ? Math.Round(g.Sum(i => i.Quantity * i.CurrentPrice) / currentValue * 100, 2)
                    : 0))
            .OrderByDescending(a => a.CurrentValue)
            .ToList();

        var dtos = items.Select(MapToDto).ToList();
        var topPerformers = dtos
            .OrderByDescending(d => d.ReturnPercentage)
            .Take(5)
            .ToList();
        var worstPerformers = dtos
            .OrderBy(d => d.ReturnPercentage)
            .Take(5)
            .ToList();

        return new InvestmentDashboardResponse(
            totalInvested, currentValue, totalProfitLoss, returnPercentage,
            allocation, topPerformers, worstPerformers);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static InvestmentDto MapToDto(Investment i)
    {
        var totalInvested = i.Quantity * i.PurchasePrice;
        var currentValue = i.Quantity * i.CurrentPrice;
        var profitLoss = currentValue - totalInvested;
        var returnPct = totalInvested > 0 ? Math.Round(profitLoss / totalInvested * 100, 2) : 0;

        return new InvestmentDto(
            Guid.Parse(i.InvestmentId),
            Guid.Parse(i.UserId),
            i.PortfolioId is not null ? Guid.Parse(i.PortfolioId) : null,
            i.AssetType.ToString().ToUpperInvariant(),
            i.AssetName,
            i.Symbol,
            i.Quantity,
            i.PurchasePrice,
            i.CurrentPrice,
            i.PurchaseDate,
            i.Status.ToString().ToUpperInvariant(),
            i.Notes,
            i.ImageUrl,
            totalInvested,
            currentValue,
            profitLoss,
            returnPct,
            i.CreatedAt,
            i.UpdatedAt);
    }
}
