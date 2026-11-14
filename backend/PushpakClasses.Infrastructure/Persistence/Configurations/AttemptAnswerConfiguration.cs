using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ToTable("AttemptAnswers");

        builder.HasKey(aa => aa.Id);

        builder.Property(aa => aa.SelectedOptionIds)
            .IsRequired();

        builder.Property(aa => aa.IsCorrect)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(aa => aa.MarksAwarded)
            .HasPrecision(5, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(aa => aa.AttemptId);
        builder.HasIndex(aa => aa.QuestionId);

        builder.HasOne(aa => aa.Attempt)
            .WithMany(a => a.AttemptAnswers)
            .HasForeignKey(aa => aa.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(aa => aa.Question)
            .WithMany()
            .HasForeignKey(aa => aa.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
