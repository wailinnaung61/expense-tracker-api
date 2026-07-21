using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IEmailNotificationService
{
    Task TrySendAsync(
        Guid userId,
        string type,
        IReadOnlyDictionary<string, string> placeholders,
        string? referenceId = null,
        string? milestone = null,
        CancellationToken ct = default);

    Task FlushPendingAsync(CancellationToken ct = default);

    Task<PagedEmailSentResult> GetHistoryAsync(
        Guid userId, string? status, int pageSize, DateTime? cursor);

    EmailSettingsResponse GetSettings(MemberEmailSettingsSnapshot snapshot);
}

public record MemberEmailSettingsSnapshot(
    bool NotifyEmailEnabled,
    NotificationPreferencesDto NotificationPreferences
);
