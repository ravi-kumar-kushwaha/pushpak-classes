using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Text)
            .IsRequired();

        builder.Property(q => q.TextHi)
            .IsRequired(false);

        builder.Property(q => q.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(q => q.Marks)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(q => q.Order)
            .IsRequired();

        builder.Property(q => q.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        // Required index: Question(QuizId, Order)
        builder.HasIndex(q => new { q.QuizId, q.Order });

        // Soft delete global query filter (AGENT_RULES.md)
        builder.HasQueryFilter(q => !q.IsDeleted);

        builder.HasOne(q => q.Quiz)
            .WithMany(quiz => quiz.Questions)
            .HasForeignKey(q => q.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(q => q.Options)
            .WithOne(o => o.Question)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
