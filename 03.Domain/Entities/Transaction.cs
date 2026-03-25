using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class Transaction
{
    public required string TransactionId { get; set; }
    public required string UserId { get; set; }
    public required AppConstants.TransactionType Type { get; set; }
    public required string CategoryId { get; set; }
    public required decimal Amount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public AppConstants.PaymentStatus Status { get; set; } = AppConstants.PaymentStatus.Completed;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string ImageUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public MemberProfile? User { get; set; }
    public ExpenseCategory? Category { get; set; }
}
