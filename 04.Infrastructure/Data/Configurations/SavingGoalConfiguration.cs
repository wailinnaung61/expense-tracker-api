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

        builder.Property(e => e.GoalName)
            .HasColumnName("goal_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(e => e.TargetAmount)
            .HasColumnName("target_amount")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(e => e.CurrentAmount)
            .HasColumnName("current_amount")
            .HasColumnType("decimal(15,2)");

        builder.Property(e => e.TargetDate)
            .HasColumnName("target_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.SavingGoalStatus>(v, true))
            .IsRequired();

        builder.Property(e => e.SavingGoalType)
            .HasColumnName("saving_goal_type")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.SavingGoalType>(v, true))
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(e => e.Icon)
            .HasColumnName("icon")
            .HasMaxLength(50);

        builder.Property(e => e.Color)
            .HasColumnName("color")
            .HasMaxLength(20);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Contributions)
            .WithOne(c => c.SavingGoal)
            .HasForeignKey(c => c.SavingGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .HasDatabaseName("ix_saving_goals_user_created");

        builder.HasIndex(e => new { e.UserId, e.Status })
            .HasDatabaseName("ix_saving_goals_user_status");
    }
}
