using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class OptionConfiguration : IEntityTypeConfiguration<Option>
{
    public void Configure(EntityTypeBuilder<Option> builder)
    {
        builder.ToTable("Options");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Text)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(o => o.TextHi)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(o => o.IsCorrect)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(o => o.QuestionId);

        builder.HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
