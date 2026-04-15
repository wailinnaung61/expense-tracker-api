using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Application.Interfaces;

public interface IBudgetReportWorkbookBuilder
{
    /// <param name="userDisplayName">Shown in the report title; falls back to a generic title if null/empty.</param>
    byte[] Build(Budget budget, string currencyCode, string? userDisplayName);
}
