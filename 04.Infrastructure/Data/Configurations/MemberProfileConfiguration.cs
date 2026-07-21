using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    public void Configure(EntityTypeBuilder<MemberProfile> builder)
    {
        builder.ToTable("member_profiles");

        builder.HasKey(m => m.UserId);

        builder.Property(m => m.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(m => m.PendingEmail)
            .HasColumnName("pending_email")
            .HasMaxLength(255);

        builder.Property(m => m.PendingEmailRequestedAt)
            .HasColumnName("pending_email_requested_at");

        builder.Property(m => m.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20);

        builder.Property(m => m.CognitoUserId)
            .HasColumnName("cognito_user_id")
            .HasMaxLength(100);

        builder.Property(m => m.CognitoUserName)
            .HasColumnName("cognito_user_name")
            .HasMaxLength(100);

        builder.Property(m => m.UserPoolId)
            .HasColumnName("user_pool_id")
            .HasMaxLength(100);

        builder.Property(m => m.MfaEnabled)
            .HasColumnName("mfa_enabled");

        builder.Property(m => m.MfaMethod)
            .HasColumnName("mfa_method")
            .HasMaxLength(50);

        builder.Property(m => m.BackUpCodes)
            .HasColumnName("backup_codes")
            .HasColumnType("text[]");

        builder.Property(m => m.RoleId)
            .HasColumnName("role_id")
            .HasMaxLength(50);

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasMaxLength(20);

        builder.Property(m => m.DailyLimit)
            .HasColumnName("daily_limit")
            .HasColumnType("decimal(15,2)");

        builder.Property(m => m.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10);

        builder.Property(m => m.Locale)
            .HasColumnName("locale")
            .HasMaxLength(10)
            .HasDefaultValue("en");

        builder.Property(m => m.NotifyBudgetAlerts)
            .HasColumnName("notify_budget_alerts")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifyRecurringPayments)
            .HasColumnName("notify_recurring_payments")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifyAutoPayments)
            .HasColumnName("notify_auto_payments")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifySavingGoals)
            .HasColumnName("notify_saving_goals")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifyLargeTransactions)
            .HasColumnName("notify_large_transactions")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifyPaymentFailures)
            .HasColumnName("notify_payment_failures")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifyExports)
            .HasColumnName("notify_exports")
            .HasDefaultValue(true);

        builder.Property(m => m.NotifyEmailEnabled)
            .HasColumnName("notify_email_enabled")
            .HasDefaultValue(false);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(m => m.LastLoginAt)
            .HasColumnName("last_login_at");
    }
}
