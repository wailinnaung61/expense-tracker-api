using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
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

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.SavingTransactionType>(v, true))
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(e => e.ContributionDate)
            .HasColumnName("contribution_date")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(e => e.MirrorTransactionId)
            .HasColumnName("mirror_transaction_id")
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at");

        builder.HasOne(e => e.SavingGoal)
            .WithMany(g => g.Contributions)
            .HasForeignKey(e => e.SavingGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.SavingGoalId, e.ContributionDate })
            .HasDatabaseName("ix_saving_goal_contributions_goal_date");

        builder.HasIndex(e => new { e.UserId, e.SavingGoalId })
            .HasDatabaseName("ix_saving_goal_contributions_user_goal");
    }
}
