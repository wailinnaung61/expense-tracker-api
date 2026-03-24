using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IMemberRepository
{
    Task<MemberProfile?> GetProfileByUserIdAsync(string userId = "");
    Task<MemberProfile> CreateProfileAsync(MemberProfile profile);
    Task<MemberProfile?> UpdateProfileAsync(MemberProfile profile);
    Task<bool> DeleteProfileAsync(string userId);
    Task<bool> UpdateLastLoginAsync(string userId, DateTime lastLoginAt);
    Task<bool> UpdateMfaSettingsAsync(string userId, bool mfaEnabled, string? mfaMethod,List<string>? backupCodes);
}
