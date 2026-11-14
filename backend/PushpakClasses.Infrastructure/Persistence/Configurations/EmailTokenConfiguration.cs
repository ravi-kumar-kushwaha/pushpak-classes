using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class EmailTokenConfiguration : IEntityTypeConfiguration<EmailToken>
{
    public void Configure(EntityTypeBuilder<EmailToken> builder)
    {
        builder.ToTable("EmailTokens");

        builder.HasKey(et => et.Id);

        builder.Property(et => et.Token)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(et => et.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(et => et.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(et => et.Token);
        builder.HasIndex(et => et.UserId);

        builder.HasOne(et => et.User)
            .WithMany(u => u.EmailTokens)
            .HasForeignKey(et => et.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
