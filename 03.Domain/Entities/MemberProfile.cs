using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class MemberProfile
{
    public required string UserId { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public string? PendingEmail { get; set; }
    public DateTime? PendingEmailRequestedAt { get; set; }
    public string? PhoneNumber { get; set; }
    public string CognitoUserId { get; set; } = string.Empty;
    public string CognitoUserName { get; set; } = string.Empty;
    public string UserPoolId { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
    public string? MfaMethod { get; set; }
    public List<string>? BackUpCodes { get; set; } = new List<string>();
    public string RoleId { get; set; } = AppConstants.Roles.User;
    public string Status { get; set; } = AppConstants.UserStatus.Active;

    public decimal DailyLimit { get; set; }
    public string Currency { get; set; } = "JPY";
    public string Locale { get; set; } = "en";

    // Notification preferences (all enabled by default)
    public bool NotifyBudgetAlerts { get; set; } = true;
    public bool NotifyRecurringPayments { get; set; } = true;
    public bool NotifyAutoPayments { get; set; } = true;
    public bool NotifySavingGoals { get; set; } = true;
    public bool NotifyLargeTransactions { get; set; } = true;
    public bool NotifyPaymentFailures { get; set; } = true;
    public bool NotifyExports { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
