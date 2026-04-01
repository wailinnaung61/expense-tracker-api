using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.DTOs;

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record SavingGoalDto(
    Guid SavingGoalId,
    Guid UserId,
    string GoalName,
    string Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal ProgressPercentage,
    decimal RemainingAmount,
    string TargetDate,
    string Status,
    string Notes,
    string ImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record SavingGoalContributionDto(
    Guid ContributionId,
    Guid SavingGoalId,
    string Type,
    decimal Amount,
    string ContributionDate,
    string Notes,
    DateTime CreatedAt
);

public record SavingDashboardResponse(
    decimal TotalSaved,
    decimal TotalTarget,
    decimal OverallProgressPercentage,
    int ActiveGoalsCount,
    int CompletedGoalsCount,
    List<SavingGoalDto> Goals
);

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateSavingGoalRequest(
    string GoalName,
    decimal TargetAmount,
    string TargetDate,
    string Description = "",
    string Notes = "",
    string ImageUrl = ""
);

public record UpdateSavingGoalRequest(
    string GoalName,
    decimal TargetAmount,
    string TargetDate,
    AppConstants.SavingGoalStatus Status = AppConstants.SavingGoalStatus.Active,
    string Description = "",
    string Notes = "",
    string ImageUrl = ""
);

public record AddSavingContributionRequest(
    AppConstants.SavingTransactionType Type,
    decimal Amount,
    string ContributionDate,
    string Notes = ""
);

public record SavingGoalFilterRequest
{
    public AppConstants.SavingGoalStatus? Status { get; init; }
    public string? Keyword { get; init; }
    public int PageSize { get; init; } = 10;
    public DateTime? Cursor { get; init; }
    public Guid? CursorId { get; init; }
}

