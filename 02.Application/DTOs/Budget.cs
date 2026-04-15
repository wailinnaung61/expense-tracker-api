using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.DTOs;

// ── Response DTOs ──────────────────────────────────────────────────────────────

public record BudgetMonthlyResponse(
    BudgetSummaryDto Summary,
    List<BudgetCategoryDto> Categories,
    List<TopSpendingDto> TopSpending,
    string? BudgetId,
    /// <summary>Inclusive budget period start (yyyy-MM-dd).</summary>
    string StartDate,
    /// <summary>Inclusive budget period end (yyyy-MM-dd).</summary>
    string EndDate
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

/// <param name="Year">Used with <paramref name="Month"/> when <paramref name="StartDate"/> and <paramref name="EndDate"/> are omitted (full calendar month).</param>
/// <param name="Month">1–12; first/last day of month become the budget range when custom dates are not sent.</param>
/// <param name="StartDate">Optional inclusive start (yyyy-MM-dd). Must be sent together with <paramref name="EndDate"/> for a custom range.</param>
/// <param name="EndDate">Optional inclusive end (yyyy-MM-dd).</param>
public record CreateBudgetRequest(
    int Year,
    int Month,
    decimal TotalAmount,
    List<CreateBudgetCategoryRequest>? Categories = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null
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
