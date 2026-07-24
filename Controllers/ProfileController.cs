using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
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
    private readonly IProfileAvatarService _avatarService;
    private readonly ILogger<ProfileController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProfileController(
        IMemberRepository memberRepository,
        IProfileAvatarService avatarService,
        ILogger<ProfileController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _memberRepository = memberRepository;
        _avatarService = avatarService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>GET /api/Profile — current user profile (includes avatar)</summary>
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);

        try
        {
            var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
            if (profile is null)
                return NotFound(new { message = _localizer["ProfileNotFound"].Value });

            return Ok(await MapResponseAsync(profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for user: {UserId}", userId);
            return ErrorResponse(_localizer["ProfileRetrieveFailed"].Value);
        }
    }

    /// <summary>PUT /api/Profile — update settings (optional AvatarPresetId)</summary>
    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileSettingsRequest request)
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);

        try
        {
            var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
            if (profile is null)
                return NotFound(new { message = _localizer["ProfileNotFound"].Value });

            if (request.PhoneNumber is not null)
                profile.PhoneNumber = request.PhoneNumber;

            if (request.Currency is not null)
            {
                if (!IsValidCurrency(request.Currency))
                    return ErrorResponse(_localizer["InvalidCurrency"].Value);
                profile.Currency = request.Currency;
            }

            if (request.DailyLimit.HasValue)
            {
                if (request.DailyLimit.Value < 0)
                    return ErrorResponse(_localizer["DailyLimitNegative"].Value);
                profile.DailyLimit = request.DailyLimit.Value;
            }

            if (request.Locale is not null)
            {
                var supported = new[] { "en", "ja", "my" };
                if (supported.Contains(request.Locale))
                    profile.Locale = request.Locale;
            }

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

            if (!string.IsNullOrWhiteSpace(request.AvatarPresetId))
            {
                try
                {
                    profile = await _avatarService.SelectPresetAsync(profile, request.AvatarPresetId);
                }
                catch (ArgumentException ex)
                {
                    return ErrorResponse(ex.Message);
                }
            }

            var updatedProfile = await _memberRepository.UpdateProfileAsync(profile);
            if (updatedProfile is null)
                return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);

            return Ok(await MapResponseAsync(updatedProfile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user: {UserId}", userId);
            return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);
        }
    }

    /// <summary>GET /api/Profile/avatars/presets — cartoon avatars to choose</summary>
    [HttpGet("avatars/presets")]
    public ActionResult<IReadOnlyList<AvatarPresetDto>> ListAvatarPresets()
    {
        return Ok(_avatarService.ListPresets(GetPublicBaseUrl()));
    }

    /// <summary>PUT /api/Profile/avatar/preset — select a cartoon preset</summary>
    [HttpPut("avatar/preset")]
    public async Task<ActionResult<ProfileResponse>> SelectAvatarPreset([FromBody] SelectAvatarPresetRequest request)
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);

        var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
        if (profile is null)
            return NotFound(new { message = _localizer["ProfileNotFound"].Value });

        try
        {
            profile = await _avatarService.SelectPresetAsync(profile, request.PresetId);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse(ex.Message);
        }

        var updated = await _memberRepository.UpdateProfileAsync(profile);
        if (updated is null)
            return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);

        return Ok(await MapResponseAsync(updated));
    }

    /// <summary>POST /api/Profile/avatar — multipart upload (field name: file)</summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProfileResponse>> UploadAvatar(IFormFile? file)
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);

        if (file is null || file.Length == 0)
            return ErrorResponse("Please choose an image file.");

        var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
        if (profile is null)
            return NotFound(new { message = _localizer["ProfileNotFound"].Value });

        try
        {
            await using var stream = file.OpenReadStream();
            profile = await _avatarService.UploadAsync(
                profile, stream, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Avatar upload failed for {UserId}", userId);
            return ErrorResponse("Avatar upload failed.");
        }

        var updated = await _memberRepository.UpdateProfileAsync(profile);
        if (updated is null)
            return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);

        return Ok(await MapResponseAsync(updated));
    }

    /// <summary>DELETE /api/Profile/avatar — remove upload and revert to cartoon preset</summary>
    [HttpDelete("avatar")]
    public async Task<ActionResult<ProfileResponse>> ClearAvatar()
    {
        var userId = UserId?.ToString();
        if (string.IsNullOrEmpty(userId))
            return ErrorResponse(_localizer["UserNotAuthenticated"].Value);

        var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
        if (profile is null)
            return NotFound(new { message = _localizer["ProfileNotFound"].Value });

        profile = await _avatarService.ClearUploadAsync(profile);
        var updated = await _memberRepository.UpdateProfileAsync(profile);
        if (updated is null)
            return ErrorResponse(_localizer["ProfileUpdateFailed"].Value);

        return Ok(await MapResponseAsync(updated));
    }

    private async Task<ProfileResponse> MapResponseAsync(MemberProfile profile)
    {
        var avatar = await _avatarService.ResolveAsync(profile, GetPublicBaseUrl());
        return new ProfileResponse(
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
            avatar,
            profile.CreatedAt,
            profile.UpdatedAt,
            profile.LastLoginAt
        );
    }

    private string GetPublicBaseUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host.Value}";
    }

    private static bool IsValidCurrency(string currency)
    {
        var supportedCurrencies = new[] { "JPY", "USD", "EUR", "GBP", "SGD", "THB", "MMK" };
        return supportedCurrencies.Contains(currency.ToUpperInvariant());
    }
}
