using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class BudgetCategoryConfiguration : IEntityTypeConfiguration<BudgetCategory>
{
    public void Configure(EntityTypeBuilder<BudgetCategory> builder)
    {
        builder.ToTable("budget_categories");

        builder.HasKey(bc => bc.BudgetCategoryId);

        builder.Property(bc => bc.BudgetCategoryId)
            .HasColumnName("budget_category_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(bc => bc.BudgetId)
            .HasColumnName("budget_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(bc => bc.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(bc => bc.AllocatedAmount)
            .HasColumnName("allocated_amount")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(bc => bc.AlertThreshold)
            .HasColumnName("alert_threshold")
            .HasColumnType("decimal(5,2)");

        builder.Property(bc => bc.IsReserved)
            .HasColumnName("is_reserved")
            .HasDefaultValue(false);

        builder.Property(bc => bc.AlertsEnabled)
            .HasColumnName("alerts_enabled")
            .HasDefaultValue(true);

        builder.Property(bc => bc.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(bc => bc.CreatedAt)
            .HasColumnName("created_at");

        builder.HasOne(bc => bc.Budget)
            .WithMany(b => b.BudgetCategories)
            .HasForeignKey(bc => bc.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bc => bc.Category)
            .WithMany()
            .HasForeignKey(bc => bc.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bc => new { bc.BudgetId, bc.CategoryId })
            .IsUnique()
            .HasDatabaseName("ix_budget_categories_budget_category");
    }
}
