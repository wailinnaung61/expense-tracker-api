namespace expense_tracker_backend.Domain.Entities;

public class InvestmentPortfolio
{
    public required string PortfolioId { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public MemberProfile? User { get; set; }
    public ICollection<Investment> Investments { get; set; } = [];
}
