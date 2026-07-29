using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedbacks");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Description).IsRequired().HasMaxLength(2000);
        builder.Property(f => f.Status).IsRequired().HasMaxLength(30);
        builder.Property(f => f.CreatedBy).HasMaxLength(100);
        builder.Property(f => f.ModifiedBy).HasMaxLength(100);

        // A feedback keeps its issue type; issue types are deactivated, never deleted.
        builder.HasOne(f => f.IssueType)
            .WithMany()
            .HasForeignKey(f => f.IssueTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.IssueTypeId).HasDatabaseName("IX_Feedbacks_IssueTypeId");
        builder.HasIndex(f => f.UserId).HasDatabaseName("IX_Feedbacks_UserId");
        builder.HasIndex(f => f.CreatedDate).HasDatabaseName("IX_Feedbacks_CreatedDate");
    }
}
