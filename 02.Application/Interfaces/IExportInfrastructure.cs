using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IExportEventPublisher
{
    Task PublishExportRequestedAsync(ExportEventDetail detail);
}

public interface IExportFileService
{
    Task<string?> GenerateDownloadUrlAsync(string s3Key, int expiryMinutes);
}
