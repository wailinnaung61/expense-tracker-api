using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.DTOs;

// ── Response DTOs ──────────────────────────────────────────────────────────────

public record BudgetMonthlyResponse(
    BudgetSummaryDto Summary,
    List<BudgetCategoryDto> Categories,
    List<TopSpendingDto> TopSpending,
    string? BudgetId
);

public record BudgetSummaryDto(
    decimal TotalBudget,
    decimal TotalSpent,
    decimal Remaining,
    decimal DailyBudget,
    int UsagePercent
);

public record BudgetCategoryDto(
    string BudgetCategoryId,
    string CategoryId,
    string Name,
    string Icon,
    string Color,
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    int UsagePercent,
    string Status,
    decimal AlertThreshold,
    int SortOrder
);

public record TopSpendingDto(
    string Name,
    int Percent
);

public record BudgetDto(
    string BudgetId,
    string PeriodType,
    string StartDate,
    string EndDate,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt
);

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateBudgetRequest(
    int Year,
    int Month,
    decimal TotalAmount,
    List<CreateBudgetCategoryRequest>? Categories = null
);

public record CreateBudgetCategoryRequest(
    string CategoryId,
    decimal AllocatedAmount,
    decimal AlertThreshold = 0.8m,
    int SortOrder = 0
);

public record UpdateBudgetRequest(
    decimal TotalAmount
);

public record UpdateBudgetCategoryRequest(
    decimal AllocatedAmount,
    decimal? AlertThreshold = null
);
