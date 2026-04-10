using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class ExportChatHandler
{
    private readonly IExportService _exportService;

    public ExportChatHandler(IExportService exportService)
    {
        _exportService = exportService;
    }

    public async Task<(string, object?)> RequestExportAsync(Guid userId, JsonElement args)
    {
        var now = DateTime.UtcNow;
        var startMonth = TryStr(args, "start_month") ?? now.ToString("yyyy-MM");
        var endMonth = TryStr(args, "end_month") ?? startMonth;

        var request = new CreateExportRequest(startMonth, endMonth);
        var result = await _exportService.RequestExportAsync(userId, request, "en");

        return ($"Export requested for {startMonth} to {endMonth}. Job ID: {result.JobId}. Status: {result.Status}. You'll be notified when it's ready to download.", result);
    }

    public async Task<(string, object?)> GetExportStatusAsync(Guid userId, JsonElement args)
    {
        var jobIdStr = TryStr(args, "job_id");
        if (!string.IsNullOrWhiteSpace(jobIdStr) && Guid.TryParse(jobIdStr, out var jobId))
        {
            var result = await _exportService.GetJobStatusAsync(userId, jobId);
            if (result is null)
                return ("Export job not found.", null);

            return result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                ? ($"Export ready! File: {result.FileName}. Use 'download export' to get the link.", result)
                : ($"Export status: {result.Status}. File: {result.FileName ?? "processing..."}.", result);
        }

        var jobs = await _exportService.GetJobsAsync(userId);
        if (jobs.Count == 0)
            return ("No export jobs found. Use 'export this month' to create one.", null);

        var latest = jobs.OrderByDescending(j => j.CreatedAt).First();
        return latest.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            ? ($"Latest export ({latest.StartMonth} to {latest.EndMonth}): Ready! File: {latest.FileName}. Job ID: {latest.JobId}", latest)
            : ($"Latest export ({latest.StartMonth} to {latest.EndMonth}): {latest.Status}. Job ID: {latest.JobId}", latest);
    }

    public async Task<(string, object?)> GetExportDownloadAsync(Guid userId, JsonElement args)
    {
        var jobIdStr = TryStr(args, "job_id");
        Guid jobId;

        if (!string.IsNullOrWhiteSpace(jobIdStr) && Guid.TryParse(jobIdStr, out jobId))
        {
            var result = await _exportService.GetDownloadUrlAsync(userId, jobId);
            if (result is null)
                return ("Export not ready or not found. Check status first.", null);

            return ($"Download link (valid 5 minutes): {result.DownloadUrl}", result);
        }

        var jobs = await _exportService.GetJobsAsync(userId);
        var completed = jobs
            .Where(j => j.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefault();

        if (completed is null)
            return ("No completed exports found. Request an export first.", null);

        var download = await _exportService.GetDownloadUrlAsync(userId, completed.JobId);
        if (download is null)
            return ("Download link not available yet. Please try again shortly.", null);

        return ($"Download link for {completed.StartMonth} to {completed.EndMonth} (valid 5 minutes): {download.DownloadUrl}", download);
    }

    public async Task<(string, object?)> ListExportsAsync(Guid userId)
    {
        var jobs = await _exportService.GetJobsAsync(userId);
        if (jobs.Count == 0)
            return ("No export jobs found.", jobs);

        var lines = jobs.OrderByDescending(j => j.CreatedAt).Take(5).Select(j =>
            $"• {j.StartMonth}–{j.EndMonth} | {j.Status} | {j.CreatedAt:yyyy-MM-dd HH:mm}{(j.FileName is not null ? $" | {j.FileName}" : "")}");
        var summary = $"Recent Exports:\n{string.Join("\n", lines)}";

        return (summary, jobs);
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;
}
