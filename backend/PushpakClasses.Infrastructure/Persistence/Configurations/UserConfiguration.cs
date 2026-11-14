using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.ProfileImageUrl)
            .HasMaxLength(500);

        builder.Property(u => u.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.IsEmailVerified)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.FailedLoginCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();

        builder.Property(u => u.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        // Unique index on Email
        builder.HasIndex(u => u.Email)
            .IsUnique();

        // Soft delete global query filter (AGENT_RULES.md)
        builder.HasQueryFilter(u => !u.IsDeleted);

        // Relationships
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.EmailTokens)
            .WithOne(et => et.User)
            .HasForeignKey(et => et.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.QuizAttempts)
            .WithOne(qa => qa.User)
            .HasForeignKey(qa => qa.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.CreatedQuizzes)
            .WithOne(q => q.CreatedByUser)
            .HasForeignKey(q => q.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.AuditLogs)
            .WithOne(al => al.User)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
