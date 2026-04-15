using System.Text.Json.Serialization;

namespace expense_tracker_backend.Application.DTOs;

// ── Request from frontend ──
public record CreateExportRequest(
    string StartMonth,
    string EndMonth
);

// ── Response to frontend ──
public record ExportJobResponse(
    Guid JobId,
    string Status,
    string Type,
    string StartMonth,
    string EndMonth,
    string? FileName,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record ExportDownloadResponse(string DownloadUrl, string FileName, DateTime ExpiresAt);

/// <summary>Returned after a budget Excel file is generated and uploaded to S3.</summary>
public record BudgetReportExcelResponse(Guid JobId, string Status, string FileName, DateTime CreatedAt);

// ── EventBridge detail payload (matches your Lambda) ──
// camelCase because your Lambda expects: userId, type, startMonth, endMonth
public class ExportEventDetail
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "excel";

    [JsonPropertyName("startMonth")]
    public string StartMonth { get; set; } = string.Empty;

    [JsonPropertyName("endMonth")]
    public string EndMonth { get; set; } = string.Empty;

    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "en";
}
