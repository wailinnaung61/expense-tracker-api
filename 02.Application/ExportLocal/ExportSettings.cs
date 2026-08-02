namespace expense_tracker_backend.Application.ExportLocal;

/// <summary>
/// Local = run Excel export in-process (no EventBridge/SQS/Lambda).
/// Production = publish EventBridge → SQS → Lambda (AWS).
/// </summary>
public class ExportSettings
{
    public const string SectionName = "Export";

    /// <summary>"Local" or "Production"</summary>
    public string Mode { get; set; } = "Local";

    public bool IsLocal =>
        string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
}
