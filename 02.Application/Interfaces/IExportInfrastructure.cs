using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IExportEventPublisher
{
    Task PublishExportRequestedAsync(ExportEventDetail detail);
}

public interface IExportFileService
{
    Task<string?> GenerateDownloadUrlAsync(string s3Key, int expiryMinutes);

    /// <summary>Upload bytes to the export bucket at the given key (overwrite if exists).</summary>
    Task UploadObjectAsync(string key, byte[] body, string contentType, CancellationToken cancellationToken = default);
}
