using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IBudgetReportService
{
    /// <summary>Builds Excel, uploads to S3, persists a completed export job. Returns null if budget not found.</summary>
    Task<BudgetReportExcelResponse?> CreateExcelReportAsync(Guid userId, string budgetId, CancellationToken cancellationToken = default);

    /// <summary>Pre-signed GET URL for a budget-excel job owned by the user.</summary>
    Task<ExportDownloadResponse?> GetReportDownloadUrlAsync(Guid userId, Guid jobId);
}
