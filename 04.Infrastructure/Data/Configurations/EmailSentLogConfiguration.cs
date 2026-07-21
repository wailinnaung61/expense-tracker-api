using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace expense_tracker_backend.Infrastructure.Persistence.Configurations;

public class EmailSentLogConfiguration : IEntityTypeConfiguration<EmailSentLog>
{
    public void Configure(EntityTypeBuilder<EmailSentLog> builder)
    {
        builder.ToTable("email_sent_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ToAddress)
            .HasColumnName("to_address")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Subject)
            .HasColumnName("subject")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.BodyHtml)
            .HasColumnName("body_html")
            .HasColumnType("text");

        builder.Property(e => e.Locale)
            .HasColumnName("locale")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Error)
            .HasColumnName("error")
            .HasMaxLength(2000);

        builder.Property(e => e.ReferenceId)
            .HasColumnName("reference_id")
            .HasMaxLength(50);

        builder.Property(e => e.Milestone)
            .HasColumnName("milestone")
            .HasMaxLength(30);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at");

        builder.HasIndex(e => new { e.UserId, e.CreatedAt });
        builder.HasIndex(e => new { e.UserId, e.Type, e.ReferenceId, e.Milestone });
        builder.HasIndex(e => e.Status);
    }
}
