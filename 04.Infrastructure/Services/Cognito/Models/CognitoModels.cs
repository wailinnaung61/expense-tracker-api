using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Infrastructure.AWS.Cognito.Models;

public record CognitoUser(
        string UserId,
        string UserName,
        string Email,
        string PhoneNumber,
        string CognitoUserId,
        string CognitoUserName,
        string UserPoolId,
        bool MfaEnabled,
        string RoleId,
        string Status,
        decimal DailyLimit,
        string Currency,
        IReadOnlyList<MenuItem> Menus
);


public record UserSignUpRequest(
    string Username,
    string Email,
    string Password
);

public record UserSignUpResponse(
    string UserId,
    bool IsConfirmed,
    string Message
);


public record UserConfirmSignUpRequest(
    string Username,
    string ConfirmationCode
);

public record ResendConfirmationRequest(
    string Username
);

public record ResendEmailVerificationRequest(
    string Username
);

public record ConfirmEmailChangeRequest(
    string ConfirmationCode
);


public record UserSignInRequest(
    string UsernameOrEmail,
    string Password
);

public record UserSignInResponse(
    string AccessToken,
    string IdToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType
);

public record UserRefreshTokenRequest(
    string RefreshToken
);


public record UserForgotPasswordRequest(
    string UsernameOrEmail
);


public record UserResetPasswordRequest(
    string UsernameOrEmail,
    string ConfirmationCode,
    string NewPassword
);

public record UserChangePasswordRequest(
    string OldPassword,
    string NewPassword
);

public record UserRefreshTokenWithUsernameRequest(
    string RefreshToken,
    string Username
);

public record UpdateProfileRequest(
    string UserName,
    string Email
);

public record MfaSetupResponse(
    string SecretCode,
    string QrCodeUri,
    string Session
);

public record MfaVerifySetupRequest(
    string TotpCode,
    string? Session = null
);

public record MfaVerifySetupResponse(
    bool Success,
    List<string> BackUpCodes,
    string Message
);

public record MfaChallengeResponse(
    string Session,
    string ChallengeName,
    string Username,
    string Message
);

public record MfaVerifyRequest(
    string Session,
    string Username,
    string TotpCode
);

public record MfaStatusResponse(
    bool MfaEnabled,
    string? PreferredMfaMethod
);

public record DisableMfaWithBackupCodeRequest(
    string Username,
    string BackupCode
);

public record DisableMfaWithBackupCodeResponse(
    bool Success,
    string Message
);


public record AuthSignInResult
{
    public bool RequiresMfa { get; init; }
    public UserSignInResponse? Tokens { get; init; }
    public MfaChallengeResponse? MfaChallenge { get; init; }
}

public record GoogleSignInRequest(
    string AuthorizationCode,
    string RedirectUri
);

public record OAuthUrlResponse(
    string AuthorizationUrl
);
