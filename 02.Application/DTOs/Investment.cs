using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.DTOs;

// ── Response DTOs ──────────────────────────────────────────────────────────────

public record InvestmentDto(
    Guid InvestmentId,
    Guid UserId,
    Guid? PortfolioId,
    string AssetType,
    string AssetName,
    string Symbol,
    decimal Quantity,
    decimal PurchasePrice,
    decimal CurrentPrice,
    string PurchaseDate,
    string Status,
    string Notes,
    string ImageUrl,
    decimal TotalInvested,
    decimal CurrentValue,
    decimal ProfitLoss,
    decimal ReturnPercentage,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record InvestmentPortfolioDto(
    Guid PortfolioId,
    Guid UserId,
    string Name,
    string Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record InvestmentDashboardResponse(
    decimal TotalInvested,
    decimal CurrentValue,
    decimal TotalProfitLoss,
    decimal ReturnPercentage,
    List<AssetAllocationDto> AssetAllocation,
    List<InvestmentDto> TopPerformers,
    List<InvestmentDto> WorstPerformers
);

public record AssetAllocationDto(
    string AssetType,
    decimal CurrentValue,
    decimal Percentage
);

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateInvestmentRequest(
    Guid? PortfolioId,
    AppConstants.AssetType AssetType,
    string AssetName,
    string Symbol,
    decimal Quantity,
    decimal PurchasePrice,
    decimal CurrentPrice,
    string PurchaseDate,
    string Notes = "",
    string ImageUrl = ""
);

public record UpdateInvestmentRequest(
    Guid? PortfolioId,
    string AssetName,
    string Symbol,
    decimal Quantity,
    decimal PurchasePrice,
    decimal CurrentPrice,
    string PurchaseDate,
    AppConstants.InvestmentStatus Status,
    string Notes = "",
    string ImageUrl = ""
);

public record InvestmentFilterRequest
{
    public Guid? PortfolioId { get; init; }
    public AppConstants.AssetType? AssetType { get; init; }
    public AppConstants.InvestmentStatus? Status { get; init; }
    public string? Keyword { get; init; }
    public int PageSize { get; init; } = 10;
    public DateTime? Cursor { get; init; }
    public Guid? CursorId { get; init; }
}

public record CreateInvestmentPortfolioRequest(
    string Name,
    string Description = ""
);

public record UpdateInvestmentPortfolioRequest(
    string Name,
    string Description,
    bool IsActive
);

