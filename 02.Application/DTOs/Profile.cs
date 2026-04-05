namespace expense_tracker_backend.Application.DTOs;

public record ProfileResponse(
    string UserId,
    string UserName,
    string Email,
    string? PhoneNumber,
    string Currency,
    string Locale,
    decimal DailyLimit,
    string RoleId,
    string Status,
    bool MfaEnabled,
    string? MfaMethod,
    NotificationPreferencesDto NotificationPreferences,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt
);

public record UpdateProfileSettingsRequest(
    string? PhoneNumber,
    string? Currency,
    string? Locale,
    decimal? DailyLimit,
    NotificationPreferencesDto? NotificationPreferences
);

public record NotificationPreferencesDto(
    bool BudgetAlerts,
    bool RecurringPayments,
    bool AutoPayments,
    bool SavingGoals,
    bool LargeTransactions,
    bool PaymentFailures,
    bool Exports
);
