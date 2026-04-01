using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class Investment
{
    public required string InvestmentId { get; set; }
    public required string UserId { get; set; }
    public string? PortfolioId { get; set; }
    public required AppConstants.AssetType AssetType { get; set; }
    public required string AssetName { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public required decimal Quantity { get; set; }
    public required decimal PurchasePrice { get; set; }
    public required decimal CurrentPrice { get; set; }
    public required string PurchaseDate { get; set; }
    public AppConstants.InvestmentStatus Status { get; set; } = AppConstants.InvestmentStatus.Holding;
    public string Notes { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? MirrorTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public MemberProfile? User { get; set; }
    public InvestmentPortfolio? Portfolio { get; set; }
}
