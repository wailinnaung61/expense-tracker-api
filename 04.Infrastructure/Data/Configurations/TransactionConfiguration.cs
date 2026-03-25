using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.TransactionId);

        builder.Property(t => t.TransactionId)
            .HasColumnName("transaction_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasColumnName("type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.TransactionType>(v, true))
            .IsRequired();

        builder.Property(t => t.CategoryId)
            .HasColumnName("category_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(t => t.CurrentAmount)
            .HasColumnName("current_amount")
            .HasColumnType("decimal(15,2)");

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(t => t.Merchant)
            .HasColumnName("merchant")
            .HasMaxLength(200);

        builder.Property(t => t.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .HasColumnName("payment_status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.PaymentStatus>(v, true));

        builder.Property(t => t.TransactionDate)
            .HasColumnName("transaction_date");

        builder.Property(t => t.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(t => t.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Performance indexes
        builder.HasIndex(t => new { t.UserId, t.TransactionDate })
            .HasDatabaseName("ix_transactions_user_date")
            .IsDescending(false, true);

        builder.HasIndex(t => new { t.UserId, t.Type, t.Status, t.TransactionDate })
            .HasDatabaseName("ix_transactions_user_type_status_date")
            .IsDescending(false, false, false, true);

        builder.HasIndex(t => new { t.UserId, t.CategoryId, t.TransactionDate })
            .HasDatabaseName("ix_transactions_user_category_date")
            .IsDescending(false, false, true);
    }
}
