using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class ExportJobRepository : IExportJobRepository
{
    private readonly ApplicationDbContext _context;

    public ExportJobRepository(ApplicationDbContext context) => _context = context;

    public async Task<ExportJob> CreateAsync(ExportJob job)
    {
        _context.ExportJobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<ExportJob?> GetByIdAsync(Guid jobId, Guid userId)
    {
        var uid = userId.ToString();
        return await _context.ExportJobs
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == uid);
    }

    public async Task<List<ExportJob>> GetByUserIdAsync(Guid userId)
    {
        var uid = userId.ToString();
        return await _context.ExportJobs
            .Where(j => j.UserId == uid)
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(Guid jobId, string status,
        string? s3Key = null, string? fileName = null, string? errorMessage = null)
    {
        var job = await _context.ExportJobs.FindAsync(jobId);
        if (job is null) return;

        job.Status = status;
        if (s3Key is not null) job.S3Key = s3Key;
        if (fileName is not null) job.FileName = fileName;
        if (errorMessage is not null) job.ErrorMessage = errorMessage;
        if (status is ExportJobStatus.Completed or ExportJobStatus.Failed)
            job.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
