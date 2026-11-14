using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("Quizzes");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(q => q.Description)
            .HasMaxLength(2000);

        builder.Property(q => q.DurationMinutes)
            .IsRequired();

        builder.Property(q => q.StartAtUtc)
            .IsRequired();

        builder.Property(q => q.EndAtUtc)
            .IsRequired();

        builder.Property(q => q.PassingPercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(q => q.MaxAttempts)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(q => q.ShuffleQuestions)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(q => q.ShuffleOptions)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(q => q.IsPublished)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(q => q.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        // Soft delete global query filter (AGENT_RULES.md)
        builder.HasQueryFilter(q => !q.IsDeleted);

        builder.HasIndex(q => q.CategoryId);
        builder.HasIndex(q => q.CreatedByUserId);
        builder.HasIndex(q => new { q.IsPublished, q.StartAtUtc, q.EndAtUtc });

        builder.HasOne(q => q.Category)
            .WithMany(c => c.Quizzes)
            .HasForeignKey(q => q.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.CreatedByUser)
            .WithMany(u => u.CreatedQuizzes)
            .HasForeignKey(q => q.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Questions)
            .WithOne(qu => qu.Quiz)
            .HasForeignKey(qu => qu.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Attempts)
            .WithOne(qa => qa.Quiz)
            .HasForeignKey(qa => qa.QuizId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
