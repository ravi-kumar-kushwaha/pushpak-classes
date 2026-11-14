using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.HasMany(c => c.Quizzes)
            .WithOne(q => q.Category)
            .HasForeignKey(q => q.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
