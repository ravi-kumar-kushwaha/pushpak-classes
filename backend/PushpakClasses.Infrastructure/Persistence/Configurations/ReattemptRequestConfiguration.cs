using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class ReattemptRequestConfiguration : IEntityTypeConfiguration<ReattemptRequest>
{
    public void Configure(EntityTypeBuilder<ReattemptRequest> builder)
    {
        builder.ToTable("ReattemptRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.ReviewNotes)
            .HasMaxLength(1000);

        builder.HasOne(r => r.User)
            .WithMany(u => u.ReattemptRequests)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Quiz)
            .WithMany(q => q.ReattemptRequests)
            .HasForeignKey(r => r.QuizId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReviewedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.QuizId, r.UserId, r.Status });
        builder.HasIndex(r => r.Status);
    }
}
