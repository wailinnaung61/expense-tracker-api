namespace expense_tracker_backend.Domain.Shared.Constants;

/// <summary>
/// Application-wide constants
/// </summary>
public static class AppConstants
{

    // User Roles
    public static class Roles
    {
        public const string Admin = "ADMIN";
        public const string User = "USER";
    }

    // User Status
    public static class UserStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Suspended = "SUSPENDED";
    }

    // MFA Methods
    public static class MfaMethods
    {
        public const string Totp = "TOTP";
    }

    public enum TransactionType
    {
        Income,
        Expense,
        Investment,
        Savings
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed
    }

    public enum RecurringFrequency
    {
        Daily,
        Weekly,
        Monthly,
        Yearly
    }

    public enum RecurringStatus
    {
        Active,
        Paused,
        Completed
    }
}
