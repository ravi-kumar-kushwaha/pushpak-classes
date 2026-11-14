using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(al => al.Id);

        builder.Property(al => al.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(al => al.EntityName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(al => al.EntityId)
            .HasMaxLength(100);

        builder.Property(al => al.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(al => al.UserId);
        builder.HasIndex(al => al.CreatedAtUtc);
        builder.HasIndex(al => new { al.EntityName, al.EntityId });

        builder.HasOne(al => al.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
