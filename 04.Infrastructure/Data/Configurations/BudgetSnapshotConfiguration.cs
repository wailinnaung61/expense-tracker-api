using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class BudgetSnapshotConfiguration : IEntityTypeConfiguration<BudgetSnapshot>
{
    public void Configure(EntityTypeBuilder<BudgetSnapshot> builder)
    {
        builder.ToTable("budget_snapshots");

        builder.HasKey(s => s.BudgetCategoryId);

        builder.Property(s => s.BudgetCategoryId)
            .HasColumnName("budget_category_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.SpentAmount)
            .HasColumnName("spent_amount")
            .HasColumnType("decimal(15,2)");

        builder.Property(s => s.TransactionCount)
            .HasColumnName("transaction_count");

        builder.Property(s => s.LastTransactionDate)
            .HasColumnName("last_transaction_date")
            .HasMaxLength(10);

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(s => s.BudgetCategory)
            .WithOne(bc => bc.Snapshot)
            .HasForeignKey<BudgetSnapshot>(s => s.BudgetCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
