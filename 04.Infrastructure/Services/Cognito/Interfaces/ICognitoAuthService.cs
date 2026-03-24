using expense_tracker_backend.Infrastructure.AWS.Cognito.Models;

namespace expense_tracker_backend.Infrastructure.AWS.Cognito.Interfaces;

public interface ICognitoAuthService
{
    Task<UserSignUpResponse> SignUpAsync(UserSignUpRequest request);
    Task<bool> ConfirmSignUpAsync(UserConfirmSignUpRequest request);
    Task<bool> ResendConfirmationCodeAsync(string username);
    Task<bool> ResendEmailVerificationAsync(string accessToken);
    Task<bool> ConfirmEmailChangeAsync(string accessToken, string confirmationCode);
    Task<AuthSignInResult> SignInAsync(UserSignInRequest request);
    Task<UserSignInResponse> RefreshTokenAsync(UserRefreshTokenWithUsernameRequest request);
    Task<bool> ForgotPasswordAsync(UserForgotPasswordRequest request);
    Task<bool> ResetPasswordAsync(UserResetPasswordRequest request);
    Task<bool> ChangePasswordAsync(string accessToken, UserChangePasswordRequest request);
    Task<bool> SignOutAsync(string accessToken);
    Task<CognitoUser?> GetUserAsync();
    Task<CognitoUser?> UpdateProfileAsync(Guid? userId, UpdateProfileRequest request, string accessToken);

    // MFA Operations
    Task<MfaSetupResponse> SetupMfaAsync(string accessToken);
    Task<MfaVerifySetupResponse> VerifyMfaSetupAsync(string accessToken, MfaVerifySetupRequest request);
    Task<UserSignInResponse> VerifyMfaCodeAsync(MfaVerifyRequest request);
    Task<bool> DisableMfaAsync(string accessToken);
    Task<bool> DisableMfaWithBackupCodeAsync(DisableMfaWithBackupCodeRequest request);
    Task<MfaStatusResponse> GetMfaStatusAsync(string accessToken);

    // Google OAuth Operations
    Task<OAuthUrlResponse> GetGoogleSignInUrlAsync(string redirectUri);
    Task<AuthSignInResult> GoogleSignInAsync(GoogleSignInRequest request);
}   
