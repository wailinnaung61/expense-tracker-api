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
    bool NotifyEmailEnabled,
    AvatarDto Avatar,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt
);

public record AvatarDto(
    string Source,
    string? PresetId,
    string Url
);

public record AvatarPresetDto(
    string Id,
    string Label,
    string AccentColor,
    string Url
);

public record SelectAvatarPresetRequest(string PresetId);

public record UpdateProfileSettingsRequest(
    string? PhoneNumber,
    string? Currency,
    string? Locale,
    decimal? DailyLimit,
    NotificationPreferencesDto? NotificationPreferences,
    bool? NotifyEmailEnabled,
    string? AvatarPresetId
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

public record EmailSentLogDto(
    Guid Id,
    string ToAddress,
    string Type,
    string Subject,
    string Locale,
    string Status,
    string? Error,
    string? ReferenceId,
    string? Milestone,
    DateTime CreatedAt,
    DateTime? SentAt
);

public record PagedEmailSentResult(
    List<EmailSentLogDto> Items,
    int TotalCount,
    bool HasNextPage,
    DateTime? NextCursor
);

public record EmailSettingsResponse(
    bool EmailFeatureEnabled,
    bool NotifyEmailEnabled,
    NotificationPreferencesDto NotificationPreferences,
    EmailTimingsDto Timings,
    QuietHoursDto QuietHours,
    IReadOnlyList<string> TemplateTypes
);

public record UpdateEmailSettingsRequest(
    bool? NotifyEmailEnabled,
    NotificationPreferencesDto? NotificationPreferences
);

public record EmailTimingsDto(
    IReadOnlyList<int> RecurringDueDaysBefore,
    bool RecurringDueOnDueDate,
    IReadOnlyList<int> RecurringOverdueDaysAfter,
    int SavingGoalDeadlineDaysBefore
);

public record QuietHoursDto(int StartHour, int EndHour);
