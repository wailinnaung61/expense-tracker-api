using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class InvestmentPortfolioConfiguration : IEntityTypeConfiguration<InvestmentPortfolio>
{
    public void Configure(EntityTypeBuilder<InvestmentPortfolio> builder)
    {
        builder.ToTable("investment_portfolios");

        builder.HasKey(p => p.PortfolioId);

        builder.Property(p => p.PortfolioId)
            .HasColumnName("portfolio_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.UserId, p.IsActive })
            .HasDatabaseName("ix_investment_portfolios_user_active");
    }
}
