using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class SavingGoalContributionConfiguration : IEntityTypeConfiguration<SavingGoalContribution>
{
    public void Configure(EntityTypeBuilder<SavingGoalContribution> builder)
    {
        builder.ToTable("saving_goal_contributions");

        builder.HasKey(e => e.ContributionId);

        builder.Property(e => e.ContributionId)
            .HasColumnName("contribution_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SavingGoalId)
            .HasColumnName("saving_goal_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(e => e.ContributionDate)
            .HasColumnName("contribution_date")
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        // Foreign key to SavingGoal (cascade delete)
        builder.HasOne(e => e.SavingGoal)
            .WithMany(g => g.Contributions)
            .HasForeignKey(e => e.SavingGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(e => new { e.SavingGoalId, e.ContributionDate })
            .HasDatabaseName("ix_saving_goal_contributions_goal_date")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.UserId, e.SavingGoalId })
            .HasDatabaseName("ix_saving_goal_contributions_user_goal");
    }
}
