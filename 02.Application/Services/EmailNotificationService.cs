using System.Text.RegularExpressions;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Options;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace expense_tracker_backend.Application.Services;

public class EmailNotificationService : IEmailNotificationService
{
    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private readonly IEmailSender _emailSender;
    private readonly IEmailSentLogRepository _logRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IEmailSender emailSender,
        IEmailSentLogRepository logRepository,
        IMemberRepository memberRepository,
        IOptions<EmailSettings> options,
        ILogger<EmailNotificationService> logger)
    {
        _emailSender = emailSender;
        _logRepository = logRepository;
        _memberRepository = memberRepository;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task TrySendAsync(
        Guid userId,
        string type,
        IReadOnlyDictionary<string, string> placeholders,
        string? referenceId = null,
        string? milestone = null,
        CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());
        if (profile is null || !profile.NotifyEmailEnabled)
            return;

        if (!IsCategoryEnabled(profile, type))
            return;

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            _logger.LogWarning("Skip email for user {UserId}: no email address", userId);
            return;
        }

        if (!string.IsNullOrEmpty(milestone)
            && await _logRepository.ExistsMilestoneAsync(userId.ToString(), type, referenceId, milestone))
        {
            return;
        }

        var locale = NormalizeLocale(profile.Locale);
        if (!TryResolveTemplate(type, locale, out var template))
        {
            _logger.LogWarning("No email template for type={Type} locale={Locale}", type, locale);
            await _logRepository.CreateAsync(new EmailSentLog
            {
                UserId = userId.ToString(),
                ToAddress = profile.Email,
                Type = type,
                Subject = type,
                Locale = locale,
                Status = EmailSentStatus.Skipped,
                Error = "Template not found",
                ReferenceId = referenceId,
                Milestone = milestone
            });
            return;
        }

        var subject = Render(template.Subject, placeholders);
        var bodyHtml = Render(template.BodyHtml, placeholders);

        var log = new EmailSentLog
        {
            UserId = userId.ToString(),
            ToAddress = profile.Email,
            Type = type,
            Subject = subject,
            BodyHtml = bodyHtml,
            Locale = locale,
            ReferenceId = referenceId,
            Milestone = milestone,
            Status = EmailSentStatus.Pending
        };

        if (IsQuietHours(DateTime.UtcNow))
        {
            await _logRepository.CreateAsync(log);
            _logger.LogInformation("Email queued (quiet hours) type={Type} user={UserId}", type, userId);
            return;
        }

        await DeliverAsync(log, ct);
    }

    public async Task FlushPendingAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        if (IsQuietHours(DateTime.UtcNow))
            return;

        var pending = await _logRepository.GetPendingAsync();
        foreach (var log in pending)
        {
            try
            {
                await DeliverAsync(log, ct, alreadyPersisted: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed flushing pending email {Id}", log.Id);
            }
        }
    }

    public async Task<PagedEmailSentResult> GetHistoryAsync(
        Guid userId, string? status, int pageSize, DateTime? cursor)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (items, totalCount) = await _logRepository.GetByUserIdAsync(userId, status, pageSize + 1, cursor);

        var hasNextPage = items.Count > pageSize;
        var resultItems = hasNextPage ? items.Take(pageSize).ToList() : items;
        var lastItem = resultItems.LastOrDefault();

        return new PagedEmailSentResult(
            resultItems.Select(MapToDto).ToList(),
            totalCount,
            hasNextPage,
            lastItem?.CreatedAt
        );
    }

    public EmailSettingsResponse GetSettings(MemberEmailSettingsSnapshot snapshot)
    {
        var templateTypes = _settings.Templates.Keys.OrderBy(k => k).ToList();
        return new EmailSettingsResponse(
            _settings.Enabled,
            snapshot.NotifyEmailEnabled,
            snapshot.NotificationPreferences,
            new EmailTimingsDto(
                _settings.Timings.RecurringDueDaysBefore,
                _settings.Timings.RecurringDueOnDueDate,
                _settings.Timings.RecurringOverdueDaysAfter,
                _settings.Timings.SavingGoalDeadlineDaysBefore),
            new QuietHoursDto(_settings.QuietHours.StartHour, _settings.QuietHours.EndHour),
            templateTypes
        );
    }

    private async Task DeliverAsync(EmailSentLog log, CancellationToken ct, bool alreadyPersisted = false)
    {
        try
        {
            await _emailSender.SendAsync(log.ToAddress, log.Subject, log.BodyHtml ?? string.Empty, ct);
            log.Status = EmailSentStatus.Sent;
            log.SentAt = DateTime.UtcNow;
            log.Error = null;
        }
        catch (Exception ex)
        {
            log.Status = EmailSentStatus.Failed;
            log.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            _logger.LogError(ex, "SMTP send failed for {To} type={Type}", log.ToAddress, log.Type);
        }

        if (alreadyPersisted)
            await _logRepository.UpdateAsync(log);
        else
            await _logRepository.CreateAsync(log);
    }

    private bool TryResolveTemplate(string type, string locale, out EmailTemplate template)
    {
        template = new EmailTemplate();
        if (!_settings.Templates.TryGetValue(type, out var byLocale) || byLocale.Count == 0)
            return false;

        if (byLocale.TryGetValue(locale, out var found) && !string.IsNullOrWhiteSpace(found.Subject))
        {
            template = found;
            return true;
        }

        if (byLocale.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en.Subject))
        {
            template = en;
            return true;
        }

        var first = byLocale.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Subject));
        if (first is null) return false;
        template = first;
        return true;
    }

    private bool IsQuietHours(DateTime utcNow)
    {
        var start = _settings.QuietHours.StartHour;
        var end = _settings.QuietHours.EndHour;
        if (start == end) return false;

        var hour = utcNow.Hour;
        if (start > end)
            return hour >= start || hour < end;
        return hour >= start && hour < end;
    }

    private static bool IsCategoryEnabled(MemberProfile profile, string type) =>
        type switch
        {
            NotificationType.BudgetThresholdReached or
            NotificationType.BudgetExceeded => profile.NotifyBudgetAlerts,

            NotificationType.RecurringPaymentDue or
            NotificationType.RecurringPaymentOverdue => profile.NotifyRecurringPayments,

            NotificationType.RecurringPaymentAutoPaid => profile.NotifyAutoPayments,

            NotificationType.SavingGoalReached or
            NotificationType.SavingGoalDeadlineNear => profile.NotifySavingGoals,

            NotificationType.LargeTransaction => profile.NotifyLargeTransactions,

            NotificationType.PaymentFailed => profile.NotifyPaymentFailures,

            NotificationType.ExportCompleted or
            NotificationType.ExportFailed => profile.NotifyExports,

            _ => true
        };

    private static string Render(string template, IReadOnlyDictionary<string, string> placeholders) =>
        PlaceholderRegex.Replace(template, m =>
            placeholders.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);

    private static string NormalizeLocale(string? locale) =>
        locale switch
        {
            "ja" => "ja",
            "my" => "my",
            _ => "en"
        };

    private static EmailSentLogDto MapToDto(EmailSentLog e) =>
        new(e.Id, e.ToAddress, e.Type, e.Subject, e.Locale, e.Status,
            e.Error, e.ReferenceId, e.Milestone, e.CreatedAt, e.SentAt);
}
