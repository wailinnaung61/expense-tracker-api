using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using expense_tracker_backend.Domain.Shared.Helpers;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Models;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using expense_tracker_backend.Infrastructure.Resources;
using expense_tracker_backend.Infrastructure.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace expense_tracker_backend.Infrastructure.AWS.Cognito.Services;

public class CognitoAuthService : ICognitoAuthService
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly CognitoSettings _cognitoSettings;
    private readonly IMemberRepository _memberRepository;
    private readonly ILogger<CognitoAuthService> _logger;
    private readonly CurrentUserService _currentUser;
    private readonly IStringLocalizer<InfrastructureResource> _localizer;

    public CognitoAuthService(
        IAmazonCognitoIdentityProvider cognitoClient,
        IOptions<AwsSettings> awsSettings,
        IMemberRepository memberRepository,
        ILogger<CognitoAuthService> logger,
        CurrentUserService currentUser,
        IStringLocalizer<InfrastructureResource> localizer)
    {
        _cognitoClient = cognitoClient ?? throw new ArgumentNullException(nameof(cognitoClient));
        _cognitoSettings = awsSettings?.Value?.Cognito ?? throw new ArgumentNullException(nameof(awsSettings));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    #region Sign Up & Confirmation

    public async Task<UserSignUpResponse> SignUpAsync(UserSignUpRequest request)
    {
        _logger.LogInformation("SignUp attempt for username: {Username}", request.Username);

        try
        {
            var signUpRequest = BuildSignUpRequest(request);
            var response = await _cognitoClient.SignUpAsync(signUpRequest);

            _logger.LogInformation(
                "SignUp successful for username: {Username}, UserSub: {UserSub}",
                request.Username,
                response.UserSub
            );

            return CreateSignUpResponse(response);
        }
        catch (Exception ex) when (IsCognitoException(ex))
        {
            HandleSignUpException(ex, request.Username);
            throw;
        }
    }

    public async Task<bool> ConfirmSignUpAsync(UserConfirmSignUpRequest request)
    {
        _logger.LogInformation("ConfirmSignUp attempt for: {Username}", request.Username);

        await ConfirmSignUpInCognitoAsync(request);
        var cognitoUser = await GetCognitoUserAsync(request.Username);
        var (email, cognitoSub) = ExtractUserAttributes(cognitoUser);

        return await CreateUserProfileIfNotExistsAsync(request.Username, email, cognitoSub);
    }

    public async Task<bool> ResendConfirmationCodeAsync(string username)
    {
        _logger.LogInformation("Resend confirmation code attempt for: {Username}", username);

        try
        {
            var resendRequest = BuildResendConfirmationRequest(username);
            await _cognitoClient.ResendConfirmationCodeAsync(resendRequest);

            _logger.LogInformation("Resend confirmation code successful for: {Username}", username);
            return true;
        }
        catch (UserNotFoundException)
        {
            _logger.LogWarning("ResendConfirmationCode attempted for non-existing user: {Username}", username);
            return true;
        }
        catch (NotAuthorizedException ex)
        {
            _logger.LogInformation(ex, "ResendConfirmationCode - user may already be confirmed: {Username}", username);
            return true;
        }
        catch (Exception ex) when (ex is InvalidParameterException or TooManyRequestsException or LimitExceededException)
        {
            _logger.LogWarning(ex, "ResendConfirmationCode failed for: {Username}", username);
            throw new InvalidOperationException(_localizer["TooManyRequests"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during resend confirmation for: {Username}", username);
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    #endregion

    #region Sign In & Authentication

    public async Task<AuthSignInResult> SignInAsync(UserSignInRequest request)
    {
        _logger.LogInformation("SignIn attempt for: {UsernameOrEmail}", request.UsernameOrEmail);

        try
        {
            var response = await InitiateAuthAsync(request.UsernameOrEmail, request.Password);

            if (!string.IsNullOrEmpty(response.ChallengeName?.Value))
            {
                return HandleAuthChallenge(response, request.UsernameOrEmail);
            }

            var auth = response.AuthenticationResult ?? throw new InvalidOperationException(_localizer["AuthenticationFailed"]);
            var subId = ExtractSubIdFromToken(auth.IdToken);

            await _memberRepository.UpdateLastLoginAsync(subId, DateTime.UtcNow);
            _logger.LogInformation("SignIn successful for: {UsernameOrEmail}", request.UsernameOrEmail);

            return CreateSuccessfulAuthResult(auth);
        }
        catch (UserNotConfirmedException)
        {
            _logger.LogWarning("User not confirmed: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["PleaseConfirmAccount"]);
        }
        catch (PasswordResetRequiredException)
        {
            _logger.LogWarning("Password reset required: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["PasswordResetRequired"]);
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("Invalid credentials for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new UnauthorizedAccessException(_localizer["InvalidCredentials"]);
        }
        catch (TooManyRequestsException)
        {
            _logger.LogWarning("Too many sign-in attempts for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["TooManyAttempts"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during sign-in for {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    public async Task<UserSignInResponse> RefreshTokenAsync(UserRefreshTokenWithUsernameRequest request)
    {
        _logger.LogInformation("RefreshToken attempt for: {Username}", request.Username);

        try
        {
            var authRequest = BuildRefreshTokenRequest(request);
            var response = await _cognitoClient.InitiateAuthAsync(authRequest);

            var auth = response.AuthenticationResult 
                ?? throw new InvalidOperationException(_localizer["RefreshTokenFailed"]);

            _logger.LogInformation("RefreshToken successful for: {Username}", request.Username);

            return new UserSignInResponse(
                auth.AccessToken,
                auth.IdToken,
                auth.RefreshToken ?? request.RefreshToken,
                auth.ExpiresIn,
                auth.TokenType
            );
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("RefreshToken failed - invalid or expired token");
            throw new UnauthorizedAccessException(_localizer["InvalidOrExpiredRefreshToken"]);
        }
        catch (UserNotFoundException)
        {
            _logger.LogWarning("RefreshToken failed - user not found: {Username}", request.Username);
            throw new UnauthorizedAccessException(_localizer["UserDoesNotExist"]);
        }
        catch (TooManyRequestsException)
        {
            _logger.LogWarning("RefreshToken throttled for: {Username}", request.Username);
            throw new InvalidOperationException(_localizer["TooManyRequests"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during refresh token for {Username}", request.Username);
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    public async Task<bool> SignOutAsync(string accessToken)
    {
        _logger.LogInformation("SignOut attempt");

        try
        {
            var signOutRequest = new GlobalSignOutRequest { AccessToken = accessToken };
            await _cognitoClient.GlobalSignOutAsync(signOutRequest);

            _logger.LogInformation("SignOut successful");
            return true;
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("SignOut failed - invalid access token");
            throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
        }
    }

    #endregion

    #region Password Management

    public async Task<bool> ForgotPasswordAsync(UserForgotPasswordRequest request)
    {
        _logger.LogInformation("ForgotPassword attempt for: {UsernameOrEmail}", request.UsernameOrEmail);

        try
        {
            var forgotRequest = BuildForgotPasswordRequest(request.UsernameOrEmail);
            await _cognitoClient.ForgotPasswordAsync(forgotRequest);

            _logger.LogInformation("ForgotPassword code sent for: {UsernameOrEmail}", request.UsernameOrEmail);
            return true;
        }
        catch (UserNotFoundException)
        {
            return true;
        }
        catch (LimitExceededException)
        {
            throw new InvalidOperationException(_localizer["TooManyRequests"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during ForgotPassword for {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["UnableToProcessForgotPassword"]);
        }
    }

    public async Task<bool> ResetPasswordAsync(UserResetPasswordRequest request)
    {
        _logger.LogInformation("ResetPassword attempt for: {UsernameOrEmail}", request.UsernameOrEmail);

        try
        {
            var confirmRequest = BuildConfirmForgotPasswordRequest(request);
            await _cognitoClient.ConfirmForgotPasswordAsync(confirmRequest);

            _logger.LogInformation("ResetPassword successful for: {UsernameOrEmail}", request.UsernameOrEmail);
            return true;
        }
        catch (CodeMismatchException)
        {
            _logger.LogWarning("ResetPassword failed - invalid code for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["InvalidConfirmationCode"]);
        }
        catch (ExpiredCodeException)
        {
            _logger.LogWarning("ResetPassword failed - expired code for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["ConfirmationCodeExpired"]);
        }
        catch (InvalidPasswordException ex)
        {
            _logger.LogWarning(ex, "ResetPassword failed - password policy violation for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["PasswordDoesNotMeetRequirements"]);
        }
        catch (UserNotFoundException)
        {
            _logger.LogWarning("ResetPassword attempted for non-existing user: {UsernameOrEmail}", request.UsernameOrEmail);
            return true;
        }
        catch (TooManyRequestsException)
        {
            _logger.LogWarning("ResetPassword rate limited for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["TooManyAttempts"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during ResetPassword for: {UsernameOrEmail}", request.UsernameOrEmail);
            throw new InvalidOperationException(_localizer["UnableToResetPassword"]);
        }
    }

    public async Task<bool> ChangePasswordAsync(string accessToken, UserChangePasswordRequest request)
    {
        _logger.LogInformation("ChangePassword attempt");

        try
        {
            await _cognitoClient.ChangePasswordAsync(new ChangePasswordRequest
            {
                AccessToken = accessToken,
                PreviousPassword = request.OldPassword,
                ProposedPassword = request.NewPassword
            });

            _logger.LogInformation("ChangePassword successful");
            return true;
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("ChangePassword failed - unauthorized");
            throw new UnauthorizedAccessException(
                _localizer["SessionExpiredOrWrongPassword"]
            );
        }
        catch (InvalidPasswordException)
        {
            throw new ArgumentException(_localizer["NewPasswordPolicyViolation"]);
        }
        catch (LimitExceededException)
        {
            throw new InvalidOperationException(_localizer["TooManyAttempts"]);
        }
    }

    #endregion

    #region User Profile Management

    public async Task<CognitoUser?> GetUserAsync()
    {
        if (!_currentUser.IsAuthenticated)
        {
            _logger.LogWarning("User not authenticated");
            return null;
        }

        if (string.IsNullOrEmpty(_currentUser.UserId))
        {
            _logger.LogWarning("Missing userId claim");
            return null;
        }

        var profile = await _memberRepository.GetProfileByUserIdAsync(_currentUser.UserId);
        if (profile == null)
            return null;

        return BuildCognitoUser(profile);
    }

    public async Task<CognitoUser?> UpdateProfileAsync(Guid? userId, UpdateProfileRequest request, string accessToken)
    {
        if (userId is null)
            return null;

        var userIdStr = userId.ToString();
        var profile = await _memberRepository.GetProfileByUserIdAsync(userIdStr??"");

        if (profile is null) 
            return null;

        try
        {
            bool emailChanged = !string.Equals(profile.Email, request.Email, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(profile.CognitoUserName))
            {
                await UpdateCognitoUserAttributesAsync(profile, request, emailChanged);
            }
            else
            {
                _logger.LogWarning("CognitoUserName missing for user {UserId}, skipping Cognito update", userIdStr);
            }

            UpdateProfileFields(profile, request, emailChanged);

            var updatedProfile = await _memberRepository.UpdateProfileAsync(profile);
            if (updatedProfile is null) 
                return null;

            return BuildCognitoUser(updatedProfile);
        }
        catch (UserNotFoundException)
        {
            _logger.LogWarning("Cognito user not found while updating profile for {UserId}", userIdStr);
            throw new InvalidOperationException(_localizer["UserNotFound"]);
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("Not authorized to update Cognito attributes for {UserId}", userIdStr);
            throw new UnauthorizedAccessException(_localizer["NotAuthorizedToUpdateProfile"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error while updating profile for {UserId}", userIdStr);
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    public async Task<bool> ConfirmEmailChangeAsync(string accessToken, string confirmationCode)
    {
        _logger.LogInformation("Confirm email change attempt");

        try
        {
            var request = new VerifyUserAttributeRequest
            {
                AccessToken = accessToken,
                AttributeName = "email",
                Code = confirmationCode
            };

            await _cognitoClient.VerifyUserAttributeAsync(request);

            if (_currentUser.IsAuthenticated && !string.IsNullOrEmpty(_currentUser.UserId))
            {
                await UpdateProfileEmailAfterConfirmationAsync(_currentUser.UserId);
            }

            _logger.LogInformation("Confirm email change successful");
            return true;
        }
        catch (CodeMismatchException)
        {
            _logger.LogWarning("ConfirmEmailChange failed - invalid code");
            throw new InvalidOperationException(_localizer["InvalidConfirmationCode"]);
        }
        catch (ExpiredCodeException)
        {
            _logger.LogWarning("ConfirmEmailChange failed - expired code");
            throw new InvalidOperationException(_localizer["ConfirmationCodeExpired"]);
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("ConfirmEmailChange failed - not authorized");
            throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during confirm email change");
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    public async Task<bool> ResendEmailVerificationAsync(string accessToken)
    {
        _logger.LogInformation("Resend email verification attempt");

        try
        {
            await _cognitoClient.GetUserAttributeVerificationCodeAsync(
                new GetUserAttributeVerificationCodeRequest
                {
                    AccessToken = accessToken,
                    AttributeName = "email"
                });

            _logger.LogInformation("Email verification code resent successfully");
            return true;
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("User not authorized to resend email verification");
            throw new UnauthorizedAccessException(_localizer["InvalidOrExpiredSession"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during resend email verification");
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    #endregion

    #region MFA Operations

    public async Task<MfaSetupResponse> SetupMfaAsync(string accessToken)
    {
        _logger.LogInformation("MFA setup initiated");

        try
        {
            var associateRequest = new AssociateSoftwareTokenRequest { AccessToken = accessToken };
            var associateResponse = await _cognitoClient.AssociateSoftwareTokenAsync(associateRequest);

            var (qrCodeUri, secretCode) = GenerateMfaQrCode(associateResponse.SecretCode);

            _logger.LogInformation("MFA setup - secret code generated");

            return new MfaSetupResponse(
                secretCode,
                qrCodeUri,
                associateResponse.Session ?? string.Empty
            );
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("MFA setup failed - invalid access token");
            throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
        }
    }

    public async Task<MfaVerifySetupResponse> VerifyMfaSetupAsync(string accessToken, MfaVerifySetupRequest request)
    {
        _logger.LogInformation("MFA setup verification initiated");

        try
        {
            var verifyRequest = BuildVerifySoftwareTokenRequest(accessToken, request);
            var verifyResponse = await _cognitoClient.VerifySoftwareTokenAsync(verifyRequest);

            if (verifyResponse.Status != VerifySoftwareTokenResponseType.SUCCESS)
            {
                _logger.LogWarning("MFA setup verification failed - invalid code");
                return new MfaVerifySetupResponse(false, new List<string>(), _localizer["InvalidVerificationCode"]);
            }

            await EnableMfaPreferenceAsync(accessToken);
            var backupCodes = await SaveMfaSettingsToDatabaseAsync();

            _logger.LogInformation("MFA setup completed successfully");
            return new MfaVerifySetupResponse(true, backupCodes, _localizer["MfaEnabledSuccessfully"]);
        }
        catch (EnableSoftwareTokenMFAException)
        {
            _logger.LogWarning("MFA setup failed - invalid TOTP code");
            throw new InvalidOperationException(_localizer["InvalidTotpCode"]);
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("MFA setup failed - invalid access token");
            throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
        }
    }

    public async Task<UserSignInResponse> VerifyMfaCodeAsync(MfaVerifyRequest request)
    {
        _logger.LogInformation("MFA code verification for: {Username}", request.Username);

        try
        {
            var challengeRequest = BuildMfaChallengeRequest(request);
            var response = await _cognitoClient.RespondToAuthChallengeAsync(challengeRequest);

            _logger.LogInformation("MFA verification successful for: {Username}", request.Username);

            return new UserSignInResponse(
                response.AuthenticationResult.AccessToken,
                response.AuthenticationResult.IdToken,
                response.AuthenticationResult.RefreshToken,
                response.AuthenticationResult.ExpiresIn,
                response.AuthenticationResult.TokenType
            );
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("MFA verification failed - invalid code for: {Username}", request.Username);
            throw new UnauthorizedAccessException(_localizer["InvalidMfaCode"]);
        }
        catch (CodeMismatchException)
        {
            _logger.LogWarning("MFA verification failed - code mismatch for: {Username}", request.Username);
            throw new InvalidOperationException(_localizer["InvalidMfaCode"]);
        }
    }

    public async Task<bool> DisableMfaAsync(string accessToken)
    {
        _logger.LogInformation("Disabling MFA");

        try
        {
            await SetMfaPreferenceAsync(accessToken, enabled: false);

            var user = await GetUserAsync();
            if (user != null)
            {
                await _memberRepository.UpdateMfaSettingsAsync(user.UserId, false, string.Empty, null);
            }

            _logger.LogInformation("MFA disabled successfully");
            return true;
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("Disable MFA failed - invalid access token");
            throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
        }
    }

    public async Task<MfaStatusResponse> GetMfaStatusAsync(string accessToken)
    {
        _logger.LogInformation("Getting MFA status");

        try
        {
            var user = await GetUserAsync();
            if (user == null)
            {
                throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
            }

            var profile = await _memberRepository.GetProfileByUserIdAsync(user.UserId);

            return new MfaStatusResponse(
                profile?.MfaEnabled ?? false,
                profile?.MfaMethod
            );
        }
        catch (NotAuthorizedException)
        {
            _logger.LogWarning("Get MFA status failed - invalid access token");
            throw new UnauthorizedAccessException(_localizer["InvalidAccessToken"]);
        }
    }

    public async Task<bool> DisableMfaWithBackupCodeAsync(DisableMfaWithBackupCodeRequest request)
    {
        _logger.LogInformation("Disable MFA with backup code attempt for: {Username}", request.Username);

        try
        {
            var userId = await GetCognitoUserSubAsync(request.Username);
            var profile = await ValidateBackupCodeAsync(userId, request.BackupCode);

            await DisableMfaInCognitoAsync(request.Username);
            await RemoveUsedBackupCodeAsync(userId, profile, request.BackupCode);

            _logger.LogInformation("MFA disabled successfully with backup code for: {Username}", request.Username);
            return true;
        }
        catch (UserNotFoundException)
        {
            _logger.LogWarning("DisableMfaWithBackupCode failed - user not found: {Username}", request.Username);
            throw new InvalidOperationException(_localizer["UserNotFound"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Cognito error during DisableMfaWithBackupCode for: {Username}", request.Username);
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    #endregion

    #region Google OAuth Operations

    public Task<OAuthUrlResponse> GetGoogleSignInUrlAsync(string redirectUri)
    {
        _logger.LogInformation("Generating Google sign-in URL");

        var authUrl = BuildGoogleOAuthUrl(redirectUri);
        return Task.FromResult(new OAuthUrlResponse(authUrl));
    }

    public async Task<AuthSignInResult> GoogleSignInAsync(GoogleSignInRequest request)
    {
        _logger.LogInformation("Google SignIn attempt");

        try
        {
            var tokenResponse = await ExchangeAuthCodeForTokensAsync(request);
            var (userId, email, userName) = ExtractGoogleUserInfo(tokenResponse.IdToken);

            await CreateGoogleUserProfileIfNeededAsync(userId, email, userName);

            _logger.LogInformation("Google SignIn successful");

            return new AuthSignInResult
            {
                RequiresMfa = false,
                Tokens = new UserSignInResponse(
                    tokenResponse.AccessToken,
                    tokenResponse.IdToken,
                    tokenResponse.RefreshToken ?? string.Empty,
                    tokenResponse.ExpiresIn,
                    "Bearer"
                )
            };
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Google SignIn error");
            throw new UnauthorizedAccessException(_localizer["GoogleSignInFailed"]);
        }
    }

    #endregion

    #region Private Helper Methods - Request Builders

    private SignUpRequest BuildSignUpRequest(UserSignUpRequest request) => new()
    {
        ClientId = _cognitoSettings.ClientId,
        Username = request.Username,
        Password = request.Password,
        SecretHash = CalculateSecretHash(request.Username),
        UserAttributes =
        [
            new AttributeType { Name = "email", Value = request.Email }
        ]
    };

    private ResendConfirmationCodeRequest BuildResendConfirmationRequest(string username) => new()
    {
        ClientId = _cognitoSettings.ClientId,
        Username = username,
        SecretHash = CalculateSecretHash(username)
    };

    private InitiateAuthRequest BuildRefreshTokenRequest(UserRefreshTokenWithUsernameRequest request) => new()
    {
        ClientId = _cognitoSettings.ClientId,
        AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
        AuthParameters = new Dictionary<string, string>
        {
            { "USERNAME", request.Username },
            { "REFRESH_TOKEN", request.RefreshToken },
            { "SECRET_HASH", CalculateSecretHash(request.Username) }
        }
    };

    private ForgotPasswordRequest BuildForgotPasswordRequest(string usernameOrEmail) => new()
    {
        ClientId = _cognitoSettings.ClientId,
        Username = usernameOrEmail,
        SecretHash = CalculateSecretHash(usernameOrEmail)
    };

    private ConfirmForgotPasswordRequest BuildConfirmForgotPasswordRequest(UserResetPasswordRequest request) => new()
    {
        ClientId = _cognitoSettings.ClientId,
        Username = request.UsernameOrEmail,
        ConfirmationCode = request.ConfirmationCode,
        Password = request.NewPassword,
        SecretHash = CalculateSecretHash(request.UsernameOrEmail)
    };

    private VerifySoftwareTokenRequest BuildVerifySoftwareTokenRequest(string accessToken, MfaVerifySetupRequest request)
    {
        var verifyRequest = new VerifySoftwareTokenRequest
        {
            AccessToken = accessToken,
            UserCode = request.TotpCode,
            FriendlyDeviceName = "Authenticator App"
        };

        if (!string.IsNullOrEmpty(request.Session))
        {
            verifyRequest.Session = request.Session;
        }

        return verifyRequest;
    }

    private RespondToAuthChallengeRequest BuildMfaChallengeRequest(MfaVerifyRequest request) => new()
    {
        ClientId = _cognitoSettings.ClientId,
        ChallengeName = ChallengeNameType.SOFTWARE_TOKEN_MFA,
        Session = request.Session,
        ChallengeResponses = new Dictionary<string, string>
        {
            { "USERNAME", request.Username },
            { "SOFTWARE_TOKEN_MFA_CODE", request.TotpCode },
            { "SECRET_HASH", CalculateSecretHash(request.Username) }
        }
    };

    #endregion

    #region Private Helper Methods - Response Builders

    private UserSignUpResponse CreateSignUpResponse(SignUpResponse response) => new(
        response.UserSub,
        response.UserConfirmed,
        response.UserConfirmed
            ? _localizer["UserRegisteredSuccessfully"]
            : _localizer["CheckEmailForConfirmationCode"]
    );

    private static AuthSignInResult CreateSuccessfulAuthResult(AuthenticationResultType auth) => new()
    {
        RequiresMfa = false,
        Tokens = new UserSignInResponse(
            auth.AccessToken,
            auth.IdToken,
            auth.RefreshToken,
            auth.ExpiresIn,
            auth.TokenType
        )
    };

    private AuthSignInResult HandleAuthChallenge(InitiateAuthResponse response, string username)
    {
        _logger.LogInformation("SignIn challenge {Challenge} for {UsernameOrEmail}", response.ChallengeName.Value, username);

        return new AuthSignInResult
        {
            RequiresMfa = true,
            MfaChallenge = new MfaChallengeResponse(
                response.Session,
                response.ChallengeName.Value,
                username,
                GetChallengeMessage(response.ChallengeName)
            )
        };
    }

    private string GetChallengeMessage(ChallengeNameType challenge) => challenge.Value switch
    {
        "SOFTWARE_TOKEN_MFA" => _localizer["ChallengeSoftwareTokenMfa"],
        "SMS_MFA" => _localizer["ChallengeSmsMfa"],
        "NEW_PASSWORD_REQUIRED" => _localizer["ChallengeNewPasswordRequired"],
        "MFA_SETUP" => _localizer["ChallengeMfaSetup"],
        _ => _localizer["ChallengeDefault"]
    };

    private CognitoUser BuildCognitoUser(MemberProfile profile)
    {
        var menus = MenuDefinitions.ResolveByRole(profile.RoleId);

        return new CognitoUser(
            profile.UserId,
            profile.UserName,
            profile.Email,
            profile.PhoneNumber ?? string.Empty,
            profile.CognitoUserId,
            profile.CognitoUserName,
            profile.UserPoolId,
            profile.MfaEnabled,
            profile.RoleId,
            profile.Status,
            profile.DailyLimit,
            profile.Currency,
            menus
        );
    }

    #endregion

    #region Private Helper Methods - Cognito Operations

    private async Task ConfirmSignUpInCognitoAsync(UserConfirmSignUpRequest request)
    {
        try
        {
            var confirmRequest = new ConfirmSignUpRequest
            {
                ClientId = _cognitoSettings.ClientId,
                Username = request.Username,
                ConfirmationCode = request.ConfirmationCode,
                SecretHash = CalculateSecretHash(request.Username)
            };

            await _cognitoClient.ConfirmSignUpAsync(confirmRequest);
            _logger.LogInformation("ConfirmSignUp successful for: {Username}", request.Username);
        }
        catch (CodeMismatchException)
        {
            _logger.LogWarning("ConfirmSignUp failed - code mismatch for: {Username}", request.Username);
            throw new InvalidOperationException(_localizer["InvalidConfirmationCode"]);
        }
        catch (ExpiredCodeException)
        {
            _logger.LogWarning("ConfirmSignUp failed - code expired for: {Username}", request.Username);
            throw new InvalidOperationException(_localizer["ConfirmationCodeExpired"]);
        }
        catch (NotAuthorizedException)
        {
            _logger.LogInformation("User already confirmed: {Username}", request.Username);
        }
    }

    private async Task<AdminGetUserResponse> GetCognitoUserAsync(string username)
    {
        try
        {
            return await _cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
            {
                UserPoolId = _cognitoSettings.UserPoolId,
                Username = username
            });
        }
        catch (UserNotFoundException)
        {
            _logger.LogWarning("User not found in Cognito after confirmation: {Username}", username);
            throw new InvalidOperationException(_localizer["UserDoesNotExist"]);
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            _logger.LogError(ex, "Failed to fetch user from Cognito: {Username}", username);
            throw new InvalidOperationException(_localizer["AuthenticationServiceError"]);
        }
    }

    private async Task<InitiateAuthResponse> InitiateAuthAsync(string username, string password)
    {
        var authRequest = new InitiateAuthRequest
        {
            ClientId = _cognitoSettings.ClientId,
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                { "USERNAME", username },
                { "PASSWORD", password },
                { "SECRET_HASH", CalculateSecretHash(username) }
            }
        };

        return await _cognitoClient.InitiateAuthAsync(authRequest);
    }

    private async Task UpdateCognitoUserAttributesAsync(MemberProfile profile, UpdateProfileRequest request, bool emailChanged)
    {
        var attributes = new List<AttributeType>
        {
            new() { Name = "name", Value = request.UserName }
        };

        if (emailChanged)
        {
            profile.PendingEmail = request.Email;
            profile.PendingEmailRequestedAt = DateTime.UtcNow;

            attributes.Add(new AttributeType { Name = "email", Value = request.Email });
            attributes.Add(new AttributeType { Name = "email_verified", Value = "false" });
        }

        var adminUpdate = new AdminUpdateUserAttributesRequest
        {
            UserPoolId = _cognitoSettings.UserPoolId,
            Username = profile.CognitoUserName,
            UserAttributes = attributes
        };

        await _cognitoClient.AdminUpdateUserAttributesAsync(adminUpdate);
    }

    private async Task EnableMfaPreferenceAsync(string accessToken)
    {
        await SetMfaPreferenceAsync(accessToken, enabled: true);
    }

    private async Task SetMfaPreferenceAsync(string accessToken, bool enabled)
    {
        var setMfaRequest = new SetUserMFAPreferenceRequest
        {
            AccessToken = accessToken,
            SoftwareTokenMfaSettings = new SoftwareTokenMfaSettingsType
            {
                Enabled = enabled,
                PreferredMfa = enabled
            }
        };

        await _cognitoClient.SetUserMFAPreferenceAsync(setMfaRequest);
    }

    private async Task DisableMfaInCognitoAsync(string username)
    {
        await _cognitoClient.AdminSetUserMFAPreferenceAsync(new AdminSetUserMFAPreferenceRequest
        {
            UserPoolId = _cognitoSettings.UserPoolId,
            Username = username,
            SoftwareTokenMfaSettings = new SoftwareTokenMfaSettingsType
            {
                Enabled = false,
                PreferredMfa = false
            }
        });
    }

    #endregion

    #region Private Helper Methods - Data Extraction

    private (string email, string cognitoSub) ExtractUserAttributes(AdminGetUserResponse user)
    {
        var email = user.UserAttributes.FirstOrDefault(a => a.Name == "email")?.Value;
        var cognitoSub = user.UserAttributes.FirstOrDefault(a => a.Name == "sub")?.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(cognitoSub))
        {
            throw new InvalidOperationException(_localizer["InvalidUserData"]);
        }

        return (email, cognitoSub);
    }

    private string ExtractSubIdFromToken(string idToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);
        var subId = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

        return string.IsNullOrEmpty(subId)
            ? throw new InvalidOperationException(_localizer["InvalidAuthenticationToken"])
            : subId;
    }

    private (string userId, string email, string userName) ExtractGoogleUserInfo(string idToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);

        var userId = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var userName = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? email;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            throw new UnauthorizedAccessException(_localizer["InvalidGoogleUserData"]);

        return (userId, email, userName);
    }

    #endregion

    #region Private Helper Methods - Database Operations

    private async Task<bool> CreateUserProfileIfNotExistsAsync(string username, string email, string cognitoSub)
    {
        var existing = await _memberRepository.GetProfileByUserIdAsync(cognitoSub);
        if (existing != null)
        {
            _logger.LogInformation("User profile already exists for CognitoSub: {CognitoSub}", cognitoSub);
            return true;
        }

        var profile = CreateMemberProfile(username, email, cognitoSub);

        try
        {
            await _memberRepository.CreateProfileAsync(profile);
            _logger.LogInformation("User profile saved to DB for: {Username}", username);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile creation skipped (likely duplicate) for CognitoSub: {CognitoSub}", cognitoSub);
            return false;
        }
    }

    private MemberProfile CreateMemberProfile(string username, string email, string cognitoSub) => new()
    {
        UserId = cognitoSub,
        UserName = username,
        Email = email,
        CognitoUserId = cognitoSub,
        CognitoUserName = username,
        UserPoolId = _cognitoSettings.UserPoolId,
        MfaEnabled = false,
        RoleId = AppConstants.Roles.User,
        Status = AppConstants.UserStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    private async Task CreateGoogleUserProfileIfNeededAsync(string userId, string email, string userName)
    {
        var existingProfile = await _memberRepository.GetProfileByUserIdAsync(userId);

        if (existingProfile != null)
            return;

        var profile = CreateMemberProfile(userName, email, userId);
        await _memberRepository.CreateProfileAsync(profile);
        _logger.LogInformation("Created profile for Google user: {Email}", email);
    }

    private async Task UpdateProfileEmailAfterConfirmationAsync(string userId)
    {
        var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
        if (profile != null && !string.IsNullOrEmpty(profile.PendingEmail))
        {
            profile.Email = profile.PendingEmail;
            profile.PendingEmail = null;
            profile.PendingEmailRequestedAt = null;
            profile.UpdatedAt = DateTime.UtcNow;

            await _memberRepository.UpdateProfileAsync(profile);
        }
    }

    private static void UpdateProfileFields(MemberProfile profile, UpdateProfileRequest request, bool emailChanged)
    {
        profile.UserName = request.UserName;

        if (emailChanged)
        {
            // Email changed - store in PendingEmail, will be updated after confirmation
            profile.PendingEmail = request.Email;
            profile.PendingEmailRequestedAt = DateTime.UtcNow;
        }
        else
        {
            // Email not changed - update normally
            profile.Email = request.Email;
        }

        profile.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<List<string>> SaveMfaSettingsToDatabaseAsync()
    {
        var user = await GetUserAsync();
        var backupCodes = new List<string>();

        if (user != null)
        {
            backupCodes = HashHelper.GenerateBackupCodes();
            await _memberRepository.UpdateMfaSettingsAsync(
                user.UserId,
                true,
                "TOTP",
                backupCodes);
        }

        return backupCodes;
    }

    private async Task<string> GetCognitoUserSubAsync(string username)
    {
        var cognitoUser = await _cognitoClient.AdminGetUserAsync(new AdminGetUserRequest
        {
            UserPoolId = _cognitoSettings.UserPoolId,
            Username = username
        });

        var userId = cognitoUser.UserAttributes.FirstOrDefault(a => a.Name == "sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User sub not found: {Username}", username);
            throw new InvalidOperationException(_localizer["UserNotFound"]);
        }

        return userId;
    }

    private async Task<MemberProfile> ValidateBackupCodeAsync(string userId, string backupCode)
    {
        var profile = await _memberRepository.GetProfileByUserIdAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("Profile not found: {UserId}", userId);
            throw new InvalidOperationException(_localizer["UserNotFound"]);
        }

        if (!profile.MfaEnabled)
        {
            _logger.LogWarning("MFA not enabled: {UserId}", userId);
            throw new InvalidOperationException(_localizer["MfaNotEnabled"]);
        }

        if (profile.BackUpCodes == null || !profile.BackUpCodes.Contains(backupCode))
        {
            _logger.LogWarning("Invalid backup code: {UserId}", userId);
            throw new InvalidOperationException(_localizer["InvalidBackupCode"]);
        }

        return profile;
    }

    private async Task RemoveUsedBackupCodeAsync(string userId, MemberProfile profile, string usedCode)
    {
        var remainingCodes = profile.BackUpCodes.Where(c => c != usedCode).ToList();
        await _memberRepository.UpdateMfaSettingsAsync(
            userId,
            false,
            null,
            remainingCodes.Count > 0 ? remainingCodes : null
        );
    }

    #endregion

    #region Private Helper Methods - OAuth & MFA

    private string BuildGoogleOAuthUrl(string redirectUri)
    {
        var cognitoDomain = $"https://{_cognitoSettings.Domain}.auth.{_cognitoSettings.Region}.amazoncognito.com";
        var state = Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _cognitoSettings.ClientId,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["redirect_uri"] = redirectUri,
            ["identity_provider"] = "Google",
            ["prompt"] = "select_account",
            ["state"] = state,
            ["nonce"] = nonce
        };

        var query = string.Join("&",
            queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{cognitoDomain}/oauth2/authorize?{query}";
    }

    private async Task<OAuthTokenResponse> ExchangeAuthCodeForTokensAsync(GoogleSignInRequest request)
    {
        using var httpClient = new HttpClient();
        var cognitoDomain = $"https://{_cognitoSettings.Domain}.auth.{_cognitoSettings.Region}.amazoncognito.com";

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", _cognitoSettings.ClientId },
            { "client_secret", _cognitoSettings.ClientSecret },
            { "code", request.AuthorizationCode },
            { "redirect_uri", request.RedirectUri }
        });

        var response = await httpClient.PostAsync($"{cognitoDomain}/oauth2/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Google SignIn failed: {Error}", error);
            throw new UnauthorizedAccessException(_localizer["GoogleSignInFailed"]);
        }

        return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>()
            ?? throw new UnauthorizedAccessException(_localizer["InvalidTokenResponse"]);
    }

    private static (string qrCodeUri, string secretCode) GenerateMfaQrCode(string secretCode)
    {
        var issuer = "ExpenseTracker";
        var label = "Account";

        var qrCodeUri =
            $"otpauth://totp/{issuer}:{Uri.EscapeDataString(label)}" +
            $"?secret={secretCode}" +
            $"&issuer={Uri.EscapeDataString(issuer)}";

        return (qrCodeUri, secretCode);
    }

    #endregion

    #region Private Helper Methods - Utilities

    private string CalculateSecretHash(string username)
    {
        if (string.IsNullOrEmpty(_cognitoSettings.ClientSecret))
            return string.Empty;

        var message = username + _cognitoSettings.ClientId;
        var keyBytes = Encoding.UTF8.GetBytes(_cognitoSettings.ClientSecret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static bool IsCognitoException(Exception ex) => ex is AmazonCognitoIdentityProviderException or ArgumentException or InvalidOperationException;

    private void HandleSignUpException(Exception ex, string username)
    {
        switch (ex)
        {
            case UsernameExistsException:
                _logger.LogWarning("SignUp failed - username already exists: {Username}", username);
                throw new InvalidOperationException(_localizer["UsernameAlreadyExists"]);
            case InvalidPasswordException:
                _logger.LogWarning(ex, "SignUp failed - invalid password for username: {Username}", username);
                throw new InvalidOperationException(_localizer["PasswordDoesNotMeetRequirements"]);
            case InvalidParameterException:
                _logger.LogWarning(ex, "SignUp failed - invalid parameter for username: {Username}", username);
                throw new InvalidOperationException(_localizer["InvalidSignUpData"]);
            case CodeDeliveryFailureException:
                _logger.LogError(ex, "SignUp failed - unable to deliver confirmation code for username: {Username}", username);
                throw new InvalidOperationException(_localizer["UnableToSendConfirmationEmail"]);
            case TooManyRequestsException:
                _logger.LogWarning(ex, "SignUp throttled for username: {Username}", username);
                throw new InvalidOperationException(_localizer["TooManyRequests"]);
            case LimitExceededException:
                _logger.LogWarning(ex, "SignUp failed - limit exceeded for username: {Username}", username);
                throw new InvalidOperationException(_localizer["SignUpLimitExceeded"]);
            case NotAuthorizedException:
                _logger.LogError(ex, "SignUp failed - not authorized (ClientId / SecretHash issue) for username: {Username}", username);
                throw new InvalidOperationException(_localizer["SignUpNotAuthorized"]);
            case AmazonCognitoIdentityProviderException:
                _logger.LogError(ex, "Cognito error during signup for username: {Username}", username);
                throw new InvalidOperationException(_localizer["AuthenticationServiceErrorRetry"]);
            default:
                _logger.LogError(ex, "Unexpected error during signup for username: {Username}", username);
                throw new InvalidOperationException(_localizer["UnexpectedSignUpError"]);
        }
    }

    #endregion

    #region Private Records

    private record OAuthTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("id_token")] string IdToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType
    );

    #endregion
}
