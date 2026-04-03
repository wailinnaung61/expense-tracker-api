using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IExportService
{
    Task<ExportJobResponse> RequestExportAsync(Guid userId, CreateExportRequest request, string locale);
    Task<ExportJobResponse?> GetJobStatusAsync(Guid userId, Guid jobId);
    Task<List<ExportJobResponse>> GetJobsAsync(Guid userId);
    Task<ExportDownloadResponse?> GetDownloadUrlAsync(Guid userId, Guid jobId);
}
