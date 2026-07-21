namespace expense_tracker_backend.Domain.Entities;

public class EmailSentLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public string Locale { get; set; } = "en";
    public string Status { get; set; } = EmailSentStatus.Pending;
    public string? Error { get; set; }
    public string? ReferenceId { get; set; }
    /// <summary>Optional milestone key for due reminders, e.g. "due_7" or "due_0".</summary>
    public string? Milestone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

public static class EmailSentStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}
