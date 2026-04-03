using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IExportJobRepository
{
    Task<ExportJob> CreateAsync(ExportJob job);
    Task<ExportJob?> GetByIdAsync(Guid jobId, Guid userId);
    Task<List<ExportJob>> GetByUserIdAsync(Guid userId);
    Task UpdateStatusAsync(Guid jobId, string status, string? s3Key = null, string? fileName = null, string? errorMessage = null);
}
