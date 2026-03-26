using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    private readonly ICognitoAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AuthController(
        ICognitoAuthService authService,
        ILogger<AuthController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _authService = authService;
        _logger = logger;
        _localizer = localizer;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<ActionResult<UserSignUpResponse>> SignUp([FromBody] UserSignUpRequest request)
    {
        _logger.LogInformation("SignUp request for username: {Username}", request.Username);

        try
        {
            var response = await _authService.SignUpAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("SignUp failed: {Message}", ex.Message);
            return ErrorResponse(ex.Message);
        }
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<ActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request)
    {
        _logger.LogInformation("ResendConfirmation request for username: {Username}", request.Username);

        try
        {
            var result = await _authService.ResendConfirmationCodeAsync(request.Username);
            return result 
                ? SuccessResponse(_localizer["ConfirmationCodeSent"]) 
                : ErrorResponse(_localizer["UnableToResendConfirmationCode"]);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("ResendConfirmation failed: {Message}", ex.Message);
            return ErrorResponse(ex.Message);
        }
    }

    [HttpPost("confirm")]
    [AllowAnonymous]
    public async Task<ActionResult> ConfirmSignUp([FromBody] UserConfirmSignUpRequest request)
    {
        _logger.LogInformation("ConfirmSignUp request for username: {Username}", request.Username);

        try
        {
            var result = await _authService.ConfirmSignUpAsync(request);
            return result
                ? SuccessResponse(_localizer["AccountConfirmedSuccessfully"])
                : ErrorResponse(_localizer["AccountConfirmationFailed"]);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("ConfirmSignUp failed: {Message}", ex.Message);
            return ErrorResponse(ex.Message);
        }
    }

    [HttpPost("signin")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthSignInResult>> SignIn([FromBody] UserSignInRequest request)
    {
        _logger.LogInformation("SignIn request for: {UsernameOrEmail}", request.UsernameOrEmail);

        try
        {
            var response = await _authService.SignInAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("SignIn unauthorized: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("SignIn failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<UserSignInResponse>> RefreshToken([FromBody] UserRefreshTokenWithUsernameRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Initiate forgot password flow. Can use username OR email.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ForgotPassword([FromBody] UserForgotPasswordRequest request)
    {
        _logger.LogInformation("ForgotPassword request for: {UsernameOrEmail}", request.UsernameOrEmail);

        await _authService.ForgotPasswordAsync(request);
        return SuccessResponse(_localizer["PasswordResetCodeSent"]);
    }

    /// <summary>
    /// Reset password with confirmation code
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ResetPassword([FromBody] UserResetPasswordRequest request)
    {
        _logger.LogInformation("ResetPassword request for: {UsernameOrEmail}", request.UsernameOrEmail);

        try
        {
            await _authService.ResetPasswordAsync(request);
            return SuccessResponse(_localizer["PasswordResetSuccessfully"]);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Change password (requires authentication)
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] UserChangePasswordRequest request)
    {
        try
        {
            await _authService.ChangePasswordAsync(GetAccessToken(), request);
            return SuccessResponse(_localizer["PasswordChangedSuccessfully"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Sign out user (invalidates all tokens globally)
    /// </summary>
    [HttpPost("signout")]
    [Authorize]
    public async Task<ActionResult> LogOut()
    {
        try
        {
            await _authService.SignOutAsync(GetAccessToken());
            return SuccessResponse(_localizer["SignedOutSuccessfully"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CognitoUser>> GetCurrentUser()
    {
        var user = await _authService.GetUserAsync();
        if (user is null)
            return Unauthorized(new { message = _localizer["InvalidToken"].Value });

        return Ok(user);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<CognitoUser>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (UserId is null) 
            return Unauthorized();

        try
        {
            var updatedProfile = await _authService.UpdateProfileAsync(UserId, request, GetAccessToken());
            return Ok(updatedProfile);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update profile failed");
            return ErrorResponse(_localizer["UnableToUpdateProfile"]);
        }
    }

    [HttpPost("resend-email-verification")]
    [Authorize]
    public async Task<ActionResult> ResendEmailVerification()
    {
        try
        {
            var result = await _authService.ResendEmailVerificationAsync(GetAccessToken());
            return result 
                ? SuccessResponse(_localizer["EmailVerificationSent"]) 
                : ErrorResponse(_localizer["UnableToResendVerificationEmail"]);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("ResendEmailVerification failed: {Message}", ex.Message);
            return ErrorResponse(ex.Message);
        }
    }

    [HttpPost("confirm-email-change")]
    [Authorize]
    public async Task<ActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
    {
        try
        {
            var result = await _authService.ConfirmEmailChangeAsync(GetAccessToken(), request.ConfirmationCode);
            return result 
                ? SuccessResponse(_localizer["EmailConfirmedAndUpdated"]) 
                : ErrorResponse(_localizer["UnableToConfirmEmail"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    #region MFA Endpoints

    /// <summary>
    /// Verify MFA code during sign-in (when MFA challenge is returned)
    /// </summary>
    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<UserSignInResponse>> VerifyMfaCode([FromBody] MfaVerifyRequest request)
    {
        try
        {
            var response = await _authService.VerifyMfaCodeAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Start MFA setup - returns secret code and QR code URI
    /// </summary>
    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa()
    {
        try
        {
            var response = await _authService.SetupMfaAsync(GetAccessToken());
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Verify MFA setup with TOTP code from authenticator app
    /// </summary>
    [HttpPost("mfa/setup/verify")]
    [Authorize]
    public async Task<ActionResult<MfaVerifySetupResponse>> VerifyMfaSetup([FromBody] MfaVerifySetupRequest request)
    {
        try
        {
            var response = await _authService.VerifyMfaSetupAsync(GetAccessToken(), request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    /// <summary>
    /// Disable MFA for current user
    /// </summary>
    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<ActionResult> DisableMfa()
    {
        try
        {
            await _authService.DisableMfaAsync(GetAccessToken());
            return SuccessResponse(_localizer["MfaDisabledSuccessfully"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Disable MFA using backup code (for recovery when user loses phone/TOTP device)
    /// </summary>
    [HttpPost("mfa/disable-with-backup")]
    [AllowAnonymous]
    public async Task<ActionResult> DisableMfaWithBackupCode([FromBody] DisableMfaWithBackupCodeRequest request)
    {
        _logger.LogInformation("DisableMfaWithBackupCode request for: {Username}", request.Username);

        try
        {
            var result = await _authService.DisableMfaWithBackupCodeAsync(request);
            return result
                ? Ok(new { success = true, message = _localizer["MfaDisabledCanSignIn"].Value })
                : BadRequest(new { success = false, message = _localizer["UnableToDisableMfa"].Value });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("DisableMfaWithBackupCode failed: {Message}", ex.Message);
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Get current MFA status
    /// </summary>
    [HttpGet("mfa/status")]
    [Authorize]
    public async Task<ActionResult<MfaStatusResponse>> GetMfaStatus()
    {
        try
        {
            var response = await _authService.GetMfaStatusAsync(GetAccessToken());
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    #endregion

    #region Google OAuth Endpoints

    /// <summary>
    /// Get Google sign-in URL - redirect user to this URL
    /// </summary>
    [HttpGet("google/url")]
    [AllowAnonymous]
    public async Task<ActionResult<OAuthUrlResponse>> GetGoogleSignInUrl([FromQuery] string redirectUri)
    {
        if (string.IsNullOrEmpty(redirectUri))
        {
            return BadRequest(new { message = _localizer["RedirectUriRequired"].Value });
        }

        var response = await _authService.GetGoogleSignInUrlAsync(redirectUri);
        return Ok(response);
    }

    /// <summary>
    /// Complete Google sign-in with authorization code
    /// </summary>
    [HttpPost("google/callback")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthSignInResult>> GoogleSignIn([FromBody] GoogleSignInRequest request)
    {
        try
        {
            var response = await _authService.GoogleSignInAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    #endregion
}
