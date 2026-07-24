using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Domain.Entities;

public class MemberProfile
{
    public required string UserId { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public string? PendingEmail { get; set; }
    public DateTime? PendingEmailRequestedAt { get; set; }
    public string? PhoneNumber { get; set; }
    public string CognitoUserId { get; set; } = string.Empty;
    public string CognitoUserName { get; set; } = string.Empty;
    public string UserPoolId { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
    public string? MfaMethod { get; set; }
    public List<string>? BackUpCodes { get; set; } = new List<string>();
    public string RoleId { get; set; } = AppConstants.Roles.User;
    public string Status { get; set; } = AppConstants.UserStatus.Active;

    public decimal DailyLimit { get; set; }
    public string Currency { get; set; } = "JPY";
    public string Locale { get; set; } = "en";

    /// <summary>"preset" (cartoon) or "upload" (custom photo).</summary>
    public string AvatarSource { get; set; } = AvatarSources.Preset;

    /// <summary>Cartoon preset id when <see cref="AvatarSource"/> is preset.</summary>
    public string AvatarPresetId { get; set; } = AvatarPresets.DefaultId;

    /// <summary>S3 key or local relative path when <see cref="AvatarSource"/> is upload.</summary>
    public string? AvatarStorageKey { get; set; }

    // Notification preferences (all enabled by default)
    public bool NotifyBudgetAlerts { get; set; } = true;
    public bool NotifyRecurringPayments { get; set; } = true;
    public bool NotifyAutoPayments { get; set; } = true;
    public bool NotifySavingGoals { get; set; } = true;
    public bool NotifyLargeTransactions { get; set; } = true;
    public bool NotifyPaymentFailures { get; set; } = true;
    public bool NotifyExports { get; set; } = true;

    /// <summary>Master switch for outbound email notifications (opt-in).</summary>
    public bool NotifyEmailEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public static class AvatarSources
{
    public const string Preset = "preset";
    public const string Upload = "upload";
}

public static class AvatarPresets
{
    public const string DefaultId = "avatar-01";

    public static IReadOnlyList<AvatarPresetDefinition> All { get; } =
    [
        new("avatar-01", "Sunny", "#F59E0B"),
        new("avatar-02", "Ocean", "#0EA5E9"),
        new("avatar-03", "Forest", "#10B981"),
        new("avatar-04", "Berry", "#EC4899"),
        new("avatar-05", "Lavender", "#8B5CF6"),
        new("avatar-06", "Coral", "#F97316"),
        new("avatar-07", "Slate", "#64748B"),
        new("avatar-08", "Mint", "#14B8A6")
    ];

    public static bool IsValid(string? id) =>
        !string.IsNullOrWhiteSpace(id) && All.Any(p => p.Id == id);

    public static string RelativePath(string presetId) => $"/avatars/presets/{presetId}.svg";
}

public record AvatarPresetDefinition(string Id, string Label, string AccentColor);
