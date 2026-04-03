using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;

namespace expense_tracker_backend.Application.Services;

public class ExportService : IExportService
{
    private readonly IExportJobRepository _repository;
    private readonly IExportEventPublisher _eventPublisher;
    private readonly IExportFileService _fileService;

    public ExportService(
        IExportJobRepository repository,
        IExportEventPublisher eventPublisher,
        IExportFileService fileService)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _fileService = fileService;
    }

    public async Task<ExportJobResponse> RequestExportAsync(Guid userId, CreateExportRequest request, string locale)
    {
        // 1. Save job to DB with PENDING status
        var job = new ExportJob
        {
            UserId = userId.ToString(),
            Type = "excel",
            StartMonth = request.StartMonth,
            EndMonth = request.EndMonth
        };
        await _repository.CreateAsync(job);

        // 2. Publish event to EventBridge → SQS → Lambda
        var detail = new ExportEventDetail
        {
            UserId = userId.ToString(),
            Type = "excel",
            StartMonth = request.StartMonth,
            EndMonth = request.EndMonth,
            JobId = job.Id.ToString(),
            Locale = locale
        };
        await _eventPublisher.PublishExportRequestedAsync(detail);

        return MapToResponse(job);
    }

    public async Task<ExportJobResponse?> GetJobStatusAsync(Guid userId, Guid jobId)
    {
        var job = await _repository.GetByIdAsync(jobId, userId);
        return job is null ? null : MapToResponse(job);
    }

    public async Task<List<ExportJobResponse>> GetJobsAsync(Guid userId)
    {
        var jobs = await _repository.GetByUserIdAsync(userId);
        return jobs.Select(MapToResponse).ToList();
    }

    public async Task<ExportDownloadResponse?> GetDownloadUrlAsync(Guid userId, Guid jobId)
    {
        var job = await _repository.GetByIdAsync(jobId, userId);
        if (job is null || job.Status != ExportJobStatus.Completed || job.S3Key is null)
            return null;

        var url = await _fileService.GenerateDownloadUrlAsync(job.S3Key, 5);
        if (url is null) return null;

        // Extract filename from S3 key: exports/{userId}/{startMonth}_{endMonth}_{timestamp}.xlsx
        var fileName = Path.GetFileName(job.S3Key);
        return new ExportDownloadResponse(url, fileName, DateTime.UtcNow.AddMinutes(5));
    }

    private static ExportJobResponse MapToResponse(ExportJob j) =>
        new(j.Id, j.Status, j.Type, j.StartMonth, j.EndMonth,
            j.S3Key is not null ? Path.GetFileName(j.S3Key) : null,
            j.CreatedAt, j.CompletedAt);
}
