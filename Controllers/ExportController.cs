using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : BaseController
{
    private readonly IExportService _exportService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(IExportService exportService, ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>POST /api/export — request async Excel export</summary>
    [HttpPost]
    public async Task<ActionResult<ExportJobResponse>> RequestExport([FromBody] CreateExportRequest request)
    {
        if (UserId is null) return Unauthorized();

        // Read locale from Accept-Language header (e.g. "ja", "en", "my")
        var locale = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim() ?? "en";

        var job = await _exportService.RequestExportAsync(UserId.Value, request, locale);
        _logger.LogInformation("Export job {JobId} queued for user {UserId}", job.JobId, UserId);
        return Accepted(job);
    }

    /// <summary>GET /api/export — list all export jobs for user</summary>
    [HttpGet]
    public async Task<ActionResult<List<ExportJobResponse>>> GetJobs()
    {
        if (UserId is null) return Unauthorized();
        var jobs = await _exportService.GetJobsAsync(UserId.Value);
        return Ok(jobs);
    }

    /// <summary>GET /api/export/{jobId} — poll job status</summary>
    [HttpGet("{jobId:guid}")]
    public async Task<ActionResult<ExportJobResponse>> GetStatus([FromRoute] Guid jobId)
    {
        if (UserId is null) return Unauthorized();
        var job = await _exportService.GetJobStatusAsync(UserId.Value, jobId);
        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>GET /api/export/{jobId}/download — pre-signed S3 URL (valid 5 min)</summary>
    [HttpGet("{jobId:guid}/download")]
    public async Task<ActionResult<ExportDownloadResponse>> GetDownloadUrl([FromRoute] Guid jobId)
    {
        if (UserId is null) return Unauthorized();
        var result = await _exportService.GetDownloadUrlAsync(UserId.Value, jobId);
        return result is null
            ? NotFound(new { message = "Export not ready or not found." })
            : Ok(result);
    }
}
