namespace expense_tracker_backend.Application.DTOs;

public record ProfileResponse(
    string UserId,
    string UserName,
    string Email,
    string? PhoneNumber,
    string Currency,
    decimal DailyLimit,
    string RoleId,
    string Status,
    bool MfaEnabled,
    string? MfaMethod,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt
);

public record UpdateProfileSettingsRequest(
    string? PhoneNumber,
    string? Currency,
    decimal? DailyLimit
);
