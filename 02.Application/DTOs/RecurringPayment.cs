using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.DTOs;

public record RecurringPaymentDto(
    string RecurringId,
    Guid UserId,
    string Name,
    decimal Amount,
    Guid CategoryId,
    string? CategoryName,
    string Frequency,
    string NextDueDate,
    string? LastPaidDate,
    int MissedCount,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateRecurringPaymentRequest(
    string Name,
    decimal Amount,
    Guid CategoryId,
    AppConstants.RecurringFrequency Frequency,
    string NextDueDate
);

public record UpdateRecurringPaymentRequest(
    string Name,
    decimal Amount,
    Guid CategoryId,
    AppConstants.RecurringFrequency Frequency,
    string NextDueDate,
    AppConstants.RecurringStatus Status
);
