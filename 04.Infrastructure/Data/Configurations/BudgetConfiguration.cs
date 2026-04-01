using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets");

        builder.HasKey(b => b.BudgetId);

        builder.Property(b => b.BudgetId)
            .HasColumnName("budget_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.PeriodType)
            .HasColumnName("period_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.BudgetPeriodType>(v, true));

        builder.Property(b => b.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date")
            .HasConversion(
                v => DateOnly.ParseExact(v, "yyyy-MM-dd"),
                v => v.ToString("yyyy-MM-dd"))
            .IsRequired();

        builder.Property(b => b.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date")
            .HasConversion(
                v => DateOnly.ParseExact(v, "yyyy-MM-dd"),
                v => v.ToString("yyyy-MM-dd"))
            .IsRequired();

        builder.Property(b => b.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.BudgetStatus>(v, true));

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.UserId, b.StartDate })
            .IsUnique()
            .HasDatabaseName("ix_budgets_user_start");
    }
}
