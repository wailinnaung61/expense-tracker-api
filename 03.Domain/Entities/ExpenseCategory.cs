using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class ExpenseCategory
{
    public required string CategoryId { get; set; }
    public required string UserId { get; set; }
    public required string DisplayName { get; set; }
    public required AppConstants.TransactionType Type { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public MemberProfile? User { get; set; }
}
