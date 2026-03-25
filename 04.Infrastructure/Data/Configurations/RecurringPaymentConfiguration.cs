using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class RecurringPaymentConfiguration : IEntityTypeConfiguration<RecurringPayment>
{
    public void Configure(EntityTypeBuilder<RecurringPayment> builder)
    {
        builder.ToTable("recurring_payments");

        builder.HasKey(r => r.RecurringId);

        builder.Property(r => r.RecurringId)
            .HasColumnName("recurring_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(r => r.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Frequency)
            .HasColumnName("frequency")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.RecurringFrequency>(v, true))
            .IsRequired();

        builder.Property(r => r.NextDueDate)
            .HasColumnName("next_due_date");

        builder.Property(r => r.LastPaidDate)
            .HasColumnName("last_paid_date");

        builder.Property(r => r.MissedCount)
            .HasColumnName("missed_count");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.RecurringStatus>(v, true));

        builder.Property(r => r.AutoPay)
            .HasColumnName("auto_pay");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Performance indexes
        builder.HasIndex(r => new { r.UserId, r.NextDueDate })
            .HasDatabaseName("ix_recurring_user_nextdue")
            .IsDescending(false, false);

        builder.HasIndex(r => new { r.UserId, r.Status, r.NextDueDate })
            .HasDatabaseName("ix_recurring_user_status_nextdue")
            .IsDescending(false, false, false);
    }
}
