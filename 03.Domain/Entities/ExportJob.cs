namespace expense_tracker_backend.Domain.Entities;

public class ExportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = ExportJobStatus.Pending;
    public string Type { get; set; } = "excel";
    public string StartMonth { get; set; } = string.Empty;
    public string EndMonth { get; set; } = string.Empty;

    /// <summary>Set when <see cref="Type"/> is a budget export (e.g. budget-excel).</summary>
    public string? BudgetId { get; set; }

    public string? S3Key { get; set; }
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public static class ExportJobStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}
