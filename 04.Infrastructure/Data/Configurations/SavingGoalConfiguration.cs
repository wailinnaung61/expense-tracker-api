using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class SavingGoalConfiguration : IEntityTypeConfiguration<SavingGoal>
{
    public void Configure(EntityTypeBuilder<SavingGoal> builder)
    {
        builder.ToTable("saving_goals");

        builder.HasKey(e => e.SavingGoalId);

        builder.Property(e => e.SavingGoalId)
            .HasColumnName("saving_goal_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.GoalName)
            .HasColumnName("goal_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TargetAmount)
            .HasColumnName("target_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.InitialDeposit)
            .HasColumnName("initial_deposit")
            .HasPrecision(18, 2);

        builder.Property(e => e.TargetDate)
            .HasColumnName("target_date")
            .IsRequired();

        builder.Property(e => e.RecurringType)
            .HasColumnName("recurring_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.RecurringFrequency>(v, true))
            .IsRequired();

        builder.Property(e => e.Icon)
            .HasColumnName("icon")
            .HasMaxLength(50);

        builder.Property(e => e.Color)
            .HasColumnName("color")
            .HasMaxLength(20);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.RecurringStatus>(v, true))
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        // Foreign keys
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .HasDatabaseName("ix_saving_goals_user_created")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.UserId, e.Status, e.TargetDate })
            .HasDatabaseName("ix_saving_goals_user_status_targetdate")
            .IsDescending(false, false, false);

        builder.HasIndex(e => new { e.UserId, e.CategoryId })
            .HasDatabaseName("ix_saving_goals_user_categoryid");
    }
}
