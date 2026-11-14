using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("QuizAttempts");

        builder.HasKey(qa => qa.Id);

        builder.Property(qa => qa.StartedAtUtc)
            .IsRequired();

        builder.Property(qa => qa.EndsAtUtc)
            .IsRequired();

        builder.Property(qa => qa.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(qa => qa.Score)
            .HasPrecision(6, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(qa => qa.TotalMarks)
            .HasPrecision(6, 2)
            .IsRequired();

        builder.Property(qa => qa.Percent)
            .HasPrecision(5, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(qa => qa.Passed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(qa => qa.AnswersJson)
            .IsRequired();

        builder.Property(qa => qa.TabSwitchCount)
            .HasDefaultValue(0)
            .IsRequired();

        // Required indexes
        // 1. QuizAttempt(QuizId, UserId)
        builder.HasIndex(qa => new { qa.QuizId, qa.UserId });

        // 2. QuizAttempt(Status, EndsAtUtc) - Essential for high concurrency background auto-submission
        builder.HasIndex(qa => new { qa.Status, qa.EndsAtUtc });

        builder.HasOne(qa => qa.Quiz)
            .WithMany(q => q.Attempts)
            .HasForeignKey(qa => qa.QuizId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(qa => qa.User)
            .WithMany(u => u.QuizAttempts)
            .HasForeignKey(qa => qa.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(qa => qa.AttemptAnswers)
            .WithOne(aa => aa.Attempt)
            .HasForeignKey(aa => aa.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
