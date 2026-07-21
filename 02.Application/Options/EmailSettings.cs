namespace expense_tracker_backend.Application.Options;

/// <summary>Binds from configuration section <c>Email</c>.</summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    public SmtpSettings Smtp { get; set; } = new();

    public QuietHoursSettings QuietHours { get; set; } = new();

    public EmailTimingSettings Timings { get; set; } = new();

    /// <summary>
    /// Shared HTML shell. Use <c>{{content}}</c> for the template body and optional <c>{{preheader}}</c>.
    /// </summary>
    public string? LayoutHtml { get; set; }

    /// <summary>
    /// Templates keyed by notification type, then locale (en/ja/my).
    /// Example: Templates["RECURRING_PAYMENT_DUE"]["en"].Subject
    /// </summary>
    public Dictionary<string, Dictionary<string, EmailTemplate>> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Spendio";
}

public class QuietHoursSettings
{
    /// <summary>Inclusive start hour (0–23) in UTC. Default 22.</summary>
    public int StartHour { get; set; } = 22;

    /// <summary>Exclusive end hour (0–23) in UTC. Default 8.</summary>
    public int EndHour { get; set; } = 8;
}

public class EmailTimingSettings
{
    public List<int> RecurringDueDaysBefore { get; set; } = [7, 3, 1];
    public bool RecurringDueOnDueDate { get; set; } = true;
    public List<int> RecurringOverdueDaysAfter { get; set; } = [1];
    public int SavingGoalDeadlineDaysBefore { get; set; } = 7;
}

public class EmailTemplate
{
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
}
