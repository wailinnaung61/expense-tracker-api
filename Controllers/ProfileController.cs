using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : BaseController
{
    private readonly IMemberRepository _memberRepository;
    private readonly ILogger<ProfileController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProfileController(
        IMemberRepository memberRepository,
        ILogger<ProfileController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _memberRepository = memberRepository;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);
        }

        _logger.LogInformation("Getting profile for user: {UserId}", userId);

        try
        {
            var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
            if (profile is null)
            {
                return NotFound(new { message = _localizer["ProfileNotFound"].Value });
            }

            return Ok(MapResponse(profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for user: {UserId}", userId);
            return ErrorResponse(_localizer["ProfileRetrieveFailed"].Value);
        }
    }

    /// <summary>
    /// Update current user's profile settings (currency, dailyLimit, phoneNumber)
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileSettingsRequest request)
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
        {
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);
        }

        _logger.LogInformation("Updating profile for user: {UserId}", userId);

        try
        {
            var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
            if (profile is null)
            {
                return NotFound(new { message = _localizer["ProfileNotFound"].Value });
            }

            if (request.PhoneNumber is not null)
            {
                profile.PhoneNumber = request.PhoneNumber;
            }

            if (request.Currency is not null)
            {
                if (!IsValidCurrency(request.Currency))
                {
                    return ErrorResponse(_localizer["InvalidCurrency"].Value);
                }
                profile.Currency = request.Currency;
            }

            if (request.DailyLimit.HasValue)
            {
                if (request.DailyLimit.Value < 0)
                {
                    return ErrorResponse(_localizer["DailyLimitNegative"].Value);
                }
                profile.DailyLimit = request.DailyLimit.Value;
            }

            if (request.Locale is not null)
            {
                var supported = new[] { "en", "ja", "my" };
                if (supported.Contains(request.Locale))
                    profile.Locale = request.Locale;
            }

            if (request.NotifyEmailEnabled.HasValue)
            {
                profile.NotifyEmailEnabled = request.NotifyEmailEnabled.Value;
            }

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

            var updatedProfile = await _memberRepository.UpdateProfileAsync(profile);
            if (updatedProfile is null)
            {
                return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);
            }

            _logger.LogInformation("Profile updated successfully for user: {UserId}", userId);
            return Ok(MapResponse(updatedProfile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user: {UserId}", userId);
            return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);
        }
    }

    private static ProfileResponse MapResponse(Domain.Entities.MemberProfile profile) =>
        new(
            profile.UserId,
            profile.UserName,
            profile.Email,
            profile.PhoneNumber,
            profile.Currency,
            profile.Locale,
            profile.DailyLimit,
            profile.RoleId,
            profile.Status,
            profile.MfaEnabled,
            profile.MfaMethod,
            new NotificationPreferencesDto(
                profile.NotifyBudgetAlerts,
                profile.NotifyRecurringPayments,
                profile.NotifyAutoPayments,
                profile.NotifySavingGoals,
                profile.NotifyLargeTransactions,
                profile.NotifyPaymentFailures,
                profile.NotifyExports),
            profile.NotifyEmailEnabled,
            profile.CreatedAt,
            profile.UpdatedAt,
            profile.LastLoginAt
        );

    private static bool IsValidCurrency(string currency)
    {
        var supportedCurrencies = new[] { "JPY", "USD", "EUR", "GBP", "SGD", "THB", "MMK" };
        return supportedCurrencies.Contains(currency.ToUpperInvariant());
    }
}
