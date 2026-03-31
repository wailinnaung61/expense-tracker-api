using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.ToTable("investments");

        builder.HasKey(i => i.InvestmentId);

        builder.Property(i => i.InvestmentId)
            .HasColumnName("investment_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.PortfolioId)
            .HasColumnName("portfolio_id")
            .HasMaxLength(50);

        builder.Property(i => i.AssetType)
            .HasColumnName("asset_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.AssetType>(v, true))
            .IsRequired();

        builder.Property(i => i.AssetName)
            .HasColumnName("asset_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(20);

        builder.Property(i => i.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("decimal(18,8)")
            .IsRequired();

        builder.Property(i => i.PurchasePrice)
            .HasColumnName("purchase_price")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(i => i.CurrentPrice)
            .HasColumnName("current_price")
            .HasColumnType("decimal(15,2)")
            .IsRequired();

        builder.Property(i => i.PurchaseDate)
            .HasColumnName("purchase_date")
            .HasColumnType("date")
            .HasConversion(
                v => DateOnly.ParseExact(v, "yyyy-MM-dd"),
                v => v.ToString("yyyy-MM-dd"))
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppConstants.InvestmentStatus>(v, true));

        builder.Property(i => i.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(i => i.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Portfolio)
            .WithMany(p => p.Investments)
            .HasForeignKey(i => i.PortfolioId)
            .OnDelete(DeleteBehavior.SetNull);

        // Performance indexes
        builder.HasIndex(i => new { i.UserId, i.AssetType })
            .HasDatabaseName("ix_investments_user_asset_type");

        builder.HasIndex(i => new { i.UserId, i.Status })
            .HasDatabaseName("ix_investments_user_status");

        builder.HasIndex(i => new { i.UserId, i.PortfolioId })
            .HasDatabaseName("ix_investments_user_portfolio");

        builder.HasIndex(i => new { i.UserId, i.PurchaseDate })
            .HasDatabaseName("ix_investments_user_purchase_date");
    }
}
