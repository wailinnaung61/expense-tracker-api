using expense_tracker_backend.Domain.Shared.Constants;
using System.Text.Json.Serialization;
namespace expense_tracker_backend.Application.DTOs;

public record ExpenseCategory(
    Guid CategoryId,
    string DisplayName,
    AppConstants.TransactionType Type,
    string Icon,
    string Color,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
    );

public record CreateExpenseCategoryDto(
   string DisplayName,
   AppConstants.TransactionType Type,
    string Icon,
    string Color
);

public record UpdateExpenseCategoryDto(
    string DisplayName,
    string Icon,
    string Color
    );

public record CategoryFilterRequest
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public AppConstants.TransactionType? Type { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Keyword { get; init; }
    public bool? IsActive { get; init; }
    public int PageSize { get; init; } = 10;
    public DateTime? Cursor { get; init; }
    public Guid? CursorId { get; init; }
}

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage { get; init; }
    public DateTime? NextCursor { get; init; }
    public Guid? NextCursorId { get; init; }
}
