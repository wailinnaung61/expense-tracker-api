using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class MemberRepository : IMemberRepository
{
    private readonly ApplicationDbContext _context;

    public MemberRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MemberProfile?> GetProfileByUserIdAsync(string userId = "")
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        return await _context.MemberProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);
    }

    public async Task<MemberProfile> CreateProfileAsync(MemberProfile profile)
    {
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.MemberProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        return profile;
    }

    public async Task<MemberProfile?> UpdateProfileAsync(MemberProfile profile)
    {
        var existing = await _context.MemberProfiles
            .FirstOrDefaultAsync(m => m.UserId == profile.UserId);

        if (existing == null)
            return null;

        existing.UserName = profile.UserName;
        existing.Email = profile.Email;
        existing.PendingEmail = profile.PendingEmail;
        existing.PendingEmailRequestedAt = profile.PendingEmailRequestedAt;
        existing.PhoneNumber = profile.PhoneNumber;
        existing.DailyLimit = profile.DailyLimit;
        existing.Currency = profile.Currency;
        existing.Status = profile.Status;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.MemberProfiles.Update(existing);
        await _context.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteProfileAsync(string userId)
    {
        var profile = await _context.MemberProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (profile == null)
            return false;

        _context.MemberProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateLastLoginAsync(string userId, DateTime lastLoginAt)
    {
        var profile = await _context.MemberProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (profile == null)
            return false;

        profile.LastLoginAt = lastLoginAt;
        profile.UpdatedAt = DateTime.UtcNow;

        _context.MemberProfiles.Update(profile);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateMfaSettingsAsync(string userId, bool mfaEnabled, string? mfaMethod, List<string>? backupCodes)
    {
        var profile = await _context.MemberProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (profile == null)
            return false;

        profile.MfaEnabled = mfaEnabled;
        profile.MfaMethod = mfaMethod;
        profile.BackUpCodes = backupCodes ?? new List<string>();
        profile.UpdatedAt = DateTime.UtcNow;

        _context.MemberProfiles.Update(profile);
        await _context.SaveChangesAsync();

        return true;
    }
}
