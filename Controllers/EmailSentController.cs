using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class EmailSentController : BaseController
{
    private readonly IEmailNotificationService _emailService;
    private readonly IMemberRepository _memberRepository;

    public EmailSentController(
        IEmailNotificationService emailService,
        IMemberRepository memberRepository)
    {
        _emailService = emailService;
        _memberRepository = memberRepository;
    }

    /// <summary>GET /api/email-sent — paginated sent/pending/failed email history</summary>
    [HttpGet("email-sent")]
    public async Task<ActionResult<PagedEmailSentResult>> GetHistory(
        [FromQuery] string? status,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? cursor = null)
    {
        if (UserId is null) return Unauthorized();
        var result = await _emailService.GetHistoryAsync(UserId.Value, status, pageSize, cursor);
        return Ok(result);
    }

    /// <summary>GET /api/email-settings — timings, template types, user email prefs (no SMTP secrets)</summary>
    [HttpGet("email-settings")]
    public async Task<ActionResult<EmailSettingsResponse>> GetSettings()
    {
        if (UserId is null) return Unauthorized();

        var profile = await _memberRepository.GetProfileByUserIdAsync(UserId.Value.ToString());
        if (profile is null) return NotFound();

        var snapshot = new MemberEmailSettingsSnapshot(
            profile.NotifyEmailEnabled,
            new NotificationPreferencesDto(
                profile.NotifyBudgetAlerts,
                profile.NotifyRecurringPayments,
                profile.NotifyAutoPayments,
                profile.NotifySavingGoals,
                profile.NotifyLargeTransactions,
                profile.NotifyPaymentFailures,
                profile.NotifyExports));

        return Ok(_emailService.GetSettings(snapshot));
    }

    /// <summary>PUT /api/email-settings — update master email switch and category prefs</summary>
    [HttpPut("email-settings")]
    public async Task<ActionResult<EmailSettingsResponse>> UpdateSettings(
        [FromBody] UpdateEmailSettingsRequest request)
    {
        if (UserId is null) return Unauthorized();

        var profile = await _memberRepository.GetProfileByUserIdAsync(UserId.Value.ToString());
        if (profile is null) return NotFound();

        if (request.NotifyEmailEnabled.HasValue)
            profile.NotifyEmailEnabled = request.NotifyEmailEnabled.Value;

        if (request.NotificationPreferences is not null)
        {
            var np = request.NotificationPreferences;
            profile.NotifyBudgetAlerts = np.BudgetAlerts;
            profile.NotifyRecurringPayments = np.RecurringPayments;
            profile.NotifyAutoPayments = np.AutoPayments;
            profile.NotifySavingGoals = np.SavingGoals;
            profile.NotifyLargeTransactions = np.LargeTransactions;
            profile.NotifyPaymentFailures = np.PaymentFailures;
            profile.NotifyExports = np.Exports;
        }

        var updated = await _memberRepository.UpdateProfileAsync(profile);
        if (updated is null) return BadRequest();

        var snapshot = new MemberEmailSettingsSnapshot(
            updated.NotifyEmailEnabled,
            new NotificationPreferencesDto(
                updated.NotifyBudgetAlerts,
                updated.NotifyRecurringPayments,
                updated.NotifyAutoPayments,
                updated.NotifySavingGoals,
                updated.NotifyLargeTransactions,
                updated.NotifyPaymentFailures,
                updated.NotifyExports));

        return Ok(_emailService.GetSettings(snapshot));
    }
}
